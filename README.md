# acs-usage-logs-fixup
A command line tool to fixup corrupted tags in Azure Communication Services usage logs

## Disclaimer

Use at your own risk!

## Usage

1. (optional) Set environment variable `AZURE_STORAGE_CONNECTION_STRING`.

1. **BACKUP**. Use Azure Storage Explorer to clone the
`insights-logs-usage` blob container.

1. Build

1. Dry-run
    ```shell
    ACSUsageLogFixup.exe <blob-container-path>
    ```

1. Full-run
    ```shell
    ACSUsageLogFixup.exe <blob-container-path> --force
    ```