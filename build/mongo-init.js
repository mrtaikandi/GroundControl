// Initialize a single-node replica set.
// This script runs only on first init (when the MongoDB data volume is empty).
// A replica set is required for change stream support, even in single-instance deployments.
rs.initiate({
  _id: "rs0",
  members: [{ _id: 0, host: "mongodb:27017" }]
});
