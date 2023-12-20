using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;

string? connStr = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");

if (string.IsNullOrEmpty(connStr))
{
    Console.WriteLine("Environment variable AZURE_STORAGE_CONNECTION_STRING is required");
    return;
}

if (args.Length != 3)
{
    Console.WriteLine("Usage: ACSUsageLogFixup.exe <month> <day> <mode>");
    Console.WriteLine();
    Console.WriteLine("Example: ACSUsageLogFixup.exe 12 11 safe");
    return;
}

string month = args[0];
string day = args[1];
string mode = args[2].ToLowerInvariant();

if (mode != "safe" && mode != "update")
{
    Console.WriteLine("Supported modes are: safe, update");
    return;
}

bool update = mode == "update";

var blobServiceClient = new BlobServiceClient(connStr);

// This tool is specifically working with the "usage" logs
string containerName = "insights-logs-usage";
var mainContainerClient = blobServiceClient.GetBlobContainerClient(containerName);

// Backups will be saved to this new container
string backupContainerName = "insights-logs-usage-backup";
var backupContainerClient = blobServiceClient.GetBlobContainerClient(backupContainerName);

Console.WriteLine($"Creating backup container if it doesn't exist: {backupContainerName}");
await backupContainerClient.CreateIfNotExistsAsync();
Console.WriteLine("Done");

// resourceId=/SUBSCRIPTIONS/<sub-id>/RESOURCEGROUPS/<rg-name>>/PROVIDERS/MICROSOFT.COMMUNICATION/COMMUNICATIONSERVICES/<resource-name>/y=<YYYY>/m=<MM>/d=<dd>/h=<hh>/m=<mm>/PT1H.json
string pattern = $"/m={month}/d={day}/";
var blobNameRegex = new Regex(pattern);

Console.WriteLine($"Collecting blobs. Pattern={pattern}");

var blobPaths = new List<string>();

await foreach (var blobItem in mainContainerClient.GetBlobsAsync())
{
    if (blobNameRegex.IsMatch(blobItem.Name) && !blobItem.Name.EndsWith("_updated.json"))
    {
        Console.WriteLine("Found: {0}", blobItem.Name);
        blobPaths.Add(blobItem.Name);
    }
}

var tagFixupRegex = new Regex("\"tags\":{.*},\"correlationVector\"");
const string replacement = "\"tags\":\"\",\"correlationVector\"";

foreach (var blobPath in blobPaths)
{
    Console.WriteLine($"Processing: {blobPath}");

    var srcBlobClient = mainContainerClient.GetBlobClient(blobPath);
    var bkpBlobClient = backupContainerClient.GetAppendBlobClient(blobPath);

    string newBlobPath = blobPath.Replace(".json", "_updated.json");
    var newBlobClient = mainContainerClient.GetAppendBlobClient(newBlobPath);

    Console.WriteLine("Preparing backup blob.");
    await bkpBlobClient.CreateAsync();

    if (update)
    {
        Console.WriteLine("Preparing updated blob.");
        await newBlobClient.CreateAsync();
    }

    using (var stream = await srcBlobClient.OpenReadAsync())
    {
        using var reader = new StreamReader(stream);

        string? line;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            // Console.WriteLine(line);

            string fixedLine = line;
            if (tagFixupRegex.IsMatch(line))
            {
                Console.WriteLine("Invalid line, fixing");
                fixedLine = tagFixupRegex.Replace(line, replacement);
            }

            object? _ = JsonSerializer.Deserialize<object>(fixedLine);
            Console.WriteLine("valid json");

            // Original line goes into the backup
            byte[] arr = Encoding.UTF8.GetBytes(line + Environment.NewLine);
            await bkpBlobClient.AppendBlockAsync(new MemoryStream(arr));

            // Fixed line goes into the _updated.json blob
            if (update)
            {
                arr = Encoding.UTF8.GetBytes(fixedLine + Environment.NewLine);
                await newBlobClient.AppendBlockAsync(new MemoryStream(arr));
            }
        }
    }

    // Delete original blob!
    if (update)
    {
        bool deleted = await srcBlobClient.DeleteIfExistsAsync();
        Console.WriteLine($"Original blob deleted? {deleted}");
    }

    Console.WriteLine("Done");
}

Console.WriteLine("All blobs done, OK");