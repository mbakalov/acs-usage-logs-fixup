# acs-usage-logs-fixup
A command line tool to fixup corrupted tags in Azure Communication Services usage logs

## Disclaimer

Use at your own risk!

## Usage

`ACSUsageLogFixup.exe <month> <day> <mode>`, where:

* `<month>` and `<day>` are used to construct the
  `/m=<month>/d=<day>/` part of the log path. Run the tool multiple times, once
  for each day to be processed.

* `mode` is either "safe" or "update"
  * "safe" means source container ("insights-logs-usage") remains
    untouched: 
    
    1. Copy each log file to a separate "insights-logs-usage-backup"
       container.

    1. Verify that we are able to update broken "tags" and that after
       the update, the value is avalid JSON.

  * "update" makes changes in the source container:

    1. Copy each log file to a separate "insights-logs-usage-backup"
       container. It will overwrite the backup created by the "safe"
       mode.

    1. Perform substituion of broken "tags" into an empty element:
       `"tags":""`

    1. Write updated log into a 2nd blob next to the original one,
       called `<originalname>_updated.json`

    1. **Delete** the `<originalname>.json`

1. Set environment variable `AZURE_STORAGE_CONNECTION_STRING`.

1. In src\ACSUsageLogFixup run: `dotnet restore`

1. Run in "safe" mode, in src\ACSUsageLogFixup:
   ```shell
   dotnet run 11 29 safe
   ```

1. Verify that backups got created and that tool reports substitions
   and no errors.

1. Run in "update" mode, in src\ACSUsageLogFixup:
   ```shell
   dotnet run 11 29 update
   ```

1. Verify that updated JSONs look good in the original container for
   that day.

1. Repeat for other months/days that need fixing.