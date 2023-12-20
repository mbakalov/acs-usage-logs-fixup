using System.Text;
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
    if (blobNameRegex.IsMatch(blobItem.Name))
    {
        Console.WriteLine("Found: {0}", blobItem.Name);
        blobPaths.Add(blobItem.Name);
    }
}

foreach (var blobPath in blobPaths)
{
    Console.WriteLine($"Processing: {blobPath}");

    var srcBlobClient = mainContainerClient.GetBlobClient(blobPath);
    
    var result = await srcBlobClient.DownloadContentAsync();
    var str = result.Value.Content.ToString();

    Console.WriteLine("Data:");
    Console.WriteLine(str);

    var dstBlobClient = backupContainerClient.GetAppendBlobClient(blobPath);
    await dstBlobClient.CreateAsync();

    byte[] arr = Encoding.UTF8.GetBytes(str);
    
    await dstBlobClient.AppendBlockAsync(new MemoryStream(arr));
    Console.WriteLine("Written to destination");
}