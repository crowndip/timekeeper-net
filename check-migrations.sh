#!/bin/bash
echo "=== Migrations in source code ==="
ls -1 src/ParentalControl.WebService/Migrations/*.cs | grep -v Designer | grep -v Snapshot

echo ""
echo "=== Checking if migration is in git ==="
git log --all --oneline --name-only | grep AddUserAliases | head -5

echo ""
echo "=== Latest commits ==="
git log --oneline -5
