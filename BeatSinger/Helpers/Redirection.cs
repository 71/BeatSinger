using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ryder.Lightweight
{
    /// <summary>
    ///   Provides the ability to redirect calls from one method to another.
    /// </summary>
    internal sealed class Redirection
    {
        /// <summary>
        ///   Methods to reference statically to prevent them from being
        ///   garbage-collected.
        /// </summary>
        [SuppressMessage("ReSharper", "CollectionNeverQueried.Local")]
        private static readonly List<MethodBase> PersistingMethods = new List<MethodBase>();

        private readonly byte[] originalBytes;
        private readonly byte[] replacementBytes;

        private readonly IntPtr originalMethodStart;

        /// <summary>
        ///   Gets the original <see cref="MethodBase"/>.
        /// </summary>
        public MethodBase Original { get; }

        /// <summary>
        ///   Gets the replacing <see cref="MethodBase"/>.
        /// </summary>
        public MethodBase Replacement { get; }

        internal Redirection(MethodBase original, MethodBase replacement, bool start)
        {
            Original = original;
            Replacement = replacement;

            // Note: I'm making local copies of the following fields to avoid accessing fields multiple times.
            RuntimeMethodHandle originalHandle = original.MethodHandle;
            RuntimeMethodHandle replacementHandle = replacement.MethodHandle;

            // Fetch their respective start
            IntPtr originalStart = Helpers.GetMethodStart(originalHandle);
            IntPtr replacementStart = Helpers.GetMethodStart(replacementHandle);

            // Edge case: calling this on the same method
            if (originalStart == replacementStart)
                throw new InvalidOperationException("Cannot redirect a method to itself.");

            // Edge case: methods are too close to one another
            int difference = (int)Math.Abs(originalStart.ToInt64() - replacementStart.ToInt64());
            int sizeOfPtr = Marshal.SizeOf(typeof(IntPtr));

            if ((sizeOfPtr == sizeof(long) && difference < 13) || (sizeOfPtr == sizeof(int) && difference < 7))
                throw new InvalidOperationException("Unable to redirect methods whose bodies are too close to one another.");

            // Make sure they're jitted
            if (!Helpers.HasBeenCompiled(originalStart))
            {
                RuntimeHelpers.PrepareMethod(originalHandle);

                originalStart = Helpers.GetMethodStart(originalHandle);
            }

            if (!Helpers.HasBeenCompiled(replacementStart))
            {
                RuntimeHelpers.PrepareMethod(replacementHandle);

                replacementStart = Helpers.GetMethodStart(replacementHandle);
            }

            // Copy local value to field
            originalMethodStart = originalStart;

            // In some cases, the memory might need to be readable / writable:
            // Make the memory region rw right away just in case.
            Helpers.AllowRW(originalStart);

            // Save bytes to change to redirect method
            byte[] replBytes = replacementBytes = Helpers.GetJmpBytes(replacementStart);
            byte[] origBytes = originalBytes = new byte[replBytes.Length];

            Marshal.Copy(originalStart, origBytes, 0, origBytes.Length);

            if (start)
            {
                CopyToStart(replBytes, originalStart);
                isRedirecting = true;
            }

            // Save methods in static array to make sure they're not garbage collected
            PersistingMethods.Add(original);
            PersistingMethods.Add(replacement);
        }

        /// <summary>
        ///   Starts redirecting calls to the replacing <see cref="MethodBase"/>.
        /// </summary>
        public void Start()
        {
            if (isRedirecting)
                return;

            CopyToStart(replacementBytes, originalMethodStart);

            isRedirecting = true;
        }

        /// <summary>
        ///   Stops redirecting calls to the replacing <see cref="MethodBase"/>.
        /// </summary>
        public void Stop()
        {
            if (!isRedirecting)
                return;

            CopyToStart(originalBytes, originalMethodStart);

            isRedirecting = false;
        }

        /// <summary>
        ///   Invokes the original method, no matter the current redirection state.
        /// </summary>
        public object InvokeOriginal(object obj, params object[] args)
        {
            IntPtr methodStart = originalMethodStart;
            bool wasRedirecting = isRedirecting;

            if (wasRedirecting)
                CopyToStart(originalBytes, methodStart);

            try
            {
                if (obj == null && Original.IsConstructor)
                    return ((ConstructorInfo)Original).Invoke(args);

                return Original.Invoke(obj, args);
            }
            finally
            {
                if (wasRedirecting)
                    CopyToStart(replacementBytes, methodStart);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Stop();

            PersistingMethods.Remove(Original);
            PersistingMethods.Remove(Replacement);
        }

        private static void CopyToStart(byte[] bytes, IntPtr methodStart) => Marshal.Copy(bytes, 0, methodStart, bytes.Length);

        bool isRedirecting;

        private static class Helpers
        {
            /// <summary>
            ///   Returns a <see cref="byte"/> array that corresponds to asm instructions
            ///   of a JMP to the <paramref name="destination"/> pointer.
            /// </summary>
            public static byte[] GetJmpBytes(IntPtr destination)
            {
                if (IntPtr.Size == sizeof(long))
                {
                    byte[] result = new byte[12];

                    result[0] = 0x48;
                    result[1] = 0xB8;
                    result[10] = 0xFF;
                    result[11] = 0xE0;

                    BitConverter.GetBytes(destination.ToInt64()).CopyTo(result, 2);

                    return result;
                }
                else
                {
                    byte[] result = new byte[6];

                    result[0] = 0x68;
                    result[5] = 0xC3;

                    BitConverter.GetBytes(destination.ToInt32()).CopyTo(result, 1);

                    return result;
                }
            }

            /// <summary>
            ///   Returns an <see cref="IntPtr"/> pointing to the start of the method's jitted body.
            /// </summary>
            public static IntPtr GetMethodStart(RuntimeMethodHandle handle)
            {
                return handle.GetFunctionPointer();
            }

            /// <summary>
            ///   Returns whether or not the specified <paramref name="methodStart"/> has
            ///   already been compiled by the JIT.
            /// </summary>
            public static bool HasBeenCompiled(IntPtr methodStart)
            {
                // According to this:
                //   https://github.com/dotnet/coreclr/blob/master/Documentation/botr/method-descriptor.md
                // An uncompiled method will look like
                //    call ...
                //    pop esi
                //    dword ...
                // In x64, that's
                //    0xE8 <short>
                //    ...
                //    0x5F 0x5E
                //
                // According to this:
                //   https://github.com/dotnet/coreclr/blob/aff5a085543f339a24a5e58f37c1641394155c45/src/vm/i386/stublinkerx86.h#L660
                // 0x5F and 0x5E below are constants...
                // According to these:
                //   http://ref.x86asm.net/coder64.html#xE8, http://ref.x86asm.net/coder32.html#xE8
                // CALL <rel32> is the same byte on both x86 and x64, so we should be good.
                //
                // Would be nice to try this on x86 though.

                const int ANALYZED_FIXUP_SIZE = 6;
                byte[] buffer = new byte[ANALYZED_FIXUP_SIZE];

                Marshal.Copy(methodStart, buffer, 0, ANALYZED_FIXUP_SIZE);

                // I don't exactly understand everything, but if I'm right, precode can be simply identified
                // by the 0xE8 byte, nothing else can start with it.
                return buffer[0] != 0xE8/* || buffer[4] != 0x5F || buffer[5] != 0x5E*/;
            }

            [DllImport("kernel32.dll")]
            private static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, int flNewProtect, out int lpflOldProtect);

            internal static void AllowRW(IntPtr address)
            {
                if (VirtualProtect(address, new UIntPtr(1), 0x40 /* PAGE_EXECUTE_READWRITE */, out var _))
                    return;

                throw new Exception($"Unable to make method memory readable and writable. Error code: {Marshal.GetLastWin32Error()}");
            }
        }
    }
}