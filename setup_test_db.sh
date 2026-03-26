#!/bin/bash
docker run --name postgres-test -p 5432:5432 -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=easy-reasy-metrics-test -d postgres:14-alpine
