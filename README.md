[![pipeline status](https://gitlab.com/GeorgeHahn/wallabag_reducer/badges/master/pipeline.svg)](https://gitlab.com/GeorgeHahn/wallabag_reducer/commits/master)

# Wallabag Reducer

A tool for automatically organizing Wallabag entries.

# Features

 - Tag entries from certain websites (currently only Goodreads)
 - Add stories from Hacker News entries

# Docker

Example docker-compose:

```
wallabag_reducer:
  image: registry.gitlab.com/georgehahn/wallabag_reducer:latest
  volumes:
    - ./wallabag_reducer:/database
  environment:
    - WALLABAG_URL=http://<wallabag_url>
    - WALLABAG_CLIENT_ID=
    - WALLABAG_CLIENT_SECRET=
    - WALLABAG_USERNAME=
    - WALLABAG_PASSWORD=
    - WALLABAG_POLL_DURATION_SECONDS=60
    - DATABASE_FILE=/database/wallabag_reducer.sqlite3
```
