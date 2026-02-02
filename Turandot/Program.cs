using Turandot;
var apiKeyPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".apikey");
await LLM.EnsureApiKey(apiKeyPath);
Console.WriteLine("Press any key to exit.");
Console.ReadKey(true);
return;
