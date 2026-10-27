using OmpOS.AgentWorker;

// Process entry point. Railway runs the worker with Serverless enabled: no timer, probe or exporter may start here.
using var app = WorkerApp.Build(args);
app.Run();
