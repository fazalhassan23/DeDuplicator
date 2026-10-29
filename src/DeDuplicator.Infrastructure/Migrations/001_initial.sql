CREATE TABLE IF NOT EXISTS scans(id TEXT PRIMARY KEY, started TEXT, state TEXT, profile TEXT);
CREATE TABLE IF NOT EXISTS files(id INTEGER PRIMARY KEY, scan TEXT, path TEXT, identity TEXT, length INTEGER, record TEXT, hash TEXT, sample TEXT, group_id INTEGER);
CREATE UNIQUE INDEX IF NOT EXISTS physical ON files(scan,identity);
CREATE INDEX IF NOT EXISTS sizes ON files(scan,length);
CREATE INDEX IF NOT EXISTS hashes ON files(scan,length,hash);
CREATE INDEX IF NOT EXISTS groups ON files(scan,group_id);
CREATE TABLE IF NOT EXISTS issues(id INTEGER PRIMARY KEY, scan TEXT, path TEXT, reason TEXT);
CREATE TABLE IF NOT EXISTS journal(id TEXT PRIMARY KEY, plan TEXT, source TEXT, destination TEXT, action TEXT, state TEXT, detail TEXT, record TEXT, created TEXT, hash TEXT);
