from bottle import abort, app, get, hook, post, request, response, route, run, static_file
from os     import chdir, environ, path
from uuid   import uuid4

import json, sqlite3

# Set cwd

app_dir = path.dirname(__file__)


# Set up database

db_exists = path.exists('app.db')
db = sqlite3.connect('app.db')

if not db_exists:
    c = db.cursor()
    c.execute('CREATE TABLE tokens(token TEXT, name TEXT)')
    c.execute('CREATE TABLE songs(id TEXT, kind INTEGER, author TEXT, content TEXT)')
    c.execute('CREATE UNIQUE INDEX songs_id ON songs(id)')

    db.commit()


# Website

mastertoken = environ.get('MASTER_TOKEN')

if mastertoken is None:
    raise Exception('Master token is not defined.')


@hook('before_request')
def authenticate():
    _, args = app().match(request.environ)

    if 'master' in args:
        master = args['master']

        if master != mastertoken:
            return abort(401, 'Unauthorized.')
    
    elif 'token' in args:
        token = args['token']

        c = db.cursor()
        c.execute('SELECT * FROM tokens WHERE token = ? LIMIT 1', (token,))

        if c.fetchone() is None:
            return abort(401, 'Unauthorized.')


@get('/')
def index():
    return static_file('pages/index.html', app_dir)

@get('/dashboard/<token>')
def dashboard(token: str):
    return static_file('pages/dashboard.html', app_dir)

@get('/<id>')
def get_song(id: str):
    c = db.cursor()
    c.execute('SELECT kind, content FROM songs WHERE id = ? LIMIT 1', (id,))

    song = c.fetchone()

    if song is None:
        return abort(404, 'Song not found.')
    else:
        response.content_type = 'application/json' if song[0] == 1 else 'text/plain'
        response.body = song[1]

@post('/upload/<token>/<id>')
def upload(token: str, id: str):
    if request.content_length > 20_000:
        return abort(400, 'Song lyrics too large.')
    
    data = (
        id, request.content_type == 'application/json', token, request.body
    )

    c = db.cursor()
    c.execute('REPLACE INTO songs(id, kind, author, content) VALUES (?, ?, ?, ?)', data)

    db.commit()

@post('/tokens/<master>/add/<name>')
def add_token(master: str, name: str):
    token = uuid4().hex

    c = db.cursor()
    c.execute('INSERT INTO tokens(token, name) VALUES (?, ?)', (token, name))

    db.commit()

    return token

@post('/tokens/<master>/revoke/<token>')
def revoke_token(master: str, token: str):
    c = db.cursor()
    c.execute('DELETE FROM tokens WHERE token = ?', (token,))

    db.commit()

@route('/tokens/<master>')
def get_tokens(master: str):
    c = db.cursor()
    c.execute('SELECT * FROM tokens')

    return json.dumps(c.fetchall())

run(host='0.0.0.0', port=environ.get('PORT', 5000))
