// Initialize a single-node replica set.
// This script runs only on first init (when the MongoDB data volume is empty).
// A replica set is required for change stream support, even in single-instance deployments.
//
// Uses localhost because docker-entrypoint-initdb.d scripts run on a temporary
// mongod instance that only binds to 127.0.0.1. The API service connects with
// directConnection=true to bypass RS topology discovery.
rs.initiate({
  _id: "rs0",
  members: [{ _id: 0, host: "localhost:27017" }]
});
