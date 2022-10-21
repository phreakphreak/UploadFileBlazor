using System.Text;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace Api;

public class UploadManager
{
    BlobContainerClient _container;
    BlobServiceClient _blobServiceClient;
    string containerName = "musicfiles";
    public UploadManager(string connectionString)
    {
        _blobServiceClient =  new BlobServiceClient(connectionString);
        _container =  this._blobServiceClient.GetBlobContainerClient(containerName);
    }
    public async Task<string> UploadFile(string path, bool overWrite, string Storage)
    {

        if (_container == null) throw new Exception("no encontrado");
        try
        {
            // Specify the StorageTransferOptions
            BlobUploadOptions options = new BlobUploadOptions
            {
                TransferOptions = new StorageTransferOptions
                {
                    // Set the maximum length of a transfer to 50MB.
                    // If the file is bigger than 50MB it will be sent in 50MB chunks.
                    MaximumTransferSize = 50 * 1024 * 1024
                }
            };

            string blobName = Path.GetFileNameWithoutExtension(path) + Path.GetExtension(path);

            BlobClient blob = _container.GetBlobClient(blobName);

            if (overWrite == true)
            {
                blob.DeleteIfExists();
            }

            using FileStream uploadFileStream = File.OpenRead(path);
            await blob.UploadAsync(uploadFileStream, options);
            uploadFileStream.Close();
            // return the url to the blob

            // if (File.Exists(path)) File.Delete(path);
            string azureBaseUrl = blobName;// $"{baseUrl}/{baseFile}/{blobName}";
            return azureBaseUrl;

        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex.Message);
            throw new Exception(ex.Message);
        }

    }

    public async Task UploadStreamAsync(Stream stream, string name, int size = 8000000)
    {
        var blob = _container.GetBlockBlobClient(name);
       var  options = new BlockBlobStageBlockOptions
        {
            TransferValidation = new UploadTransferValidationOptions()
            {
                // Set the maximum length of a transfer to 50MB.
                // If the file is bigger than 50MB it will be sent in 50MB chunks.
                // ChecksumAlgorithm = "  50 * 1024 * 102
                
            }
        };

 
        // local variable to track the current number of bytes read into buffer
        int bytesRead;
 
        // track the current block number as the code iterates through the file
        int blockNumber = 0;
 
        // Create list to track blockIds, it will be needed after the loop
        List<string> blockList = new List<string>();
 
        do {
            // increment block number by 1 each iteration
            blockNumber++; 
             
            // set block ID as a string and convert it to Base64 which is the required format
            string blockId = $"{blockNumber:0000000}";
            string base64BlockId = Convert.ToBase64String(Encoding.UTF8.GetBytes(blockId));
 
            // create buffer and retrieve chunk
            byte[] buffer = new byte[size];
            bytesRead = await stream.ReadAsync(buffer, 0, size);
 
            // Upload buffer chunk to Azure
            await blob.StageBlockAsync(base64BlockId, new MemoryStream(buffer, 0, bytesRead), null);
 
            // add the current blockId into our list
            blockList.Add(base64BlockId); 
 
            // While bytesRead == size it means there is more data left to read and process
        } while (bytesRead == size); 
 
        // add the blockList to the Azure which allows the resource to stick together the chunks
        await blob.CommitBlockListAsync(blockList);
 
        // make sure to dispose the stream once your are done
        stream.Dispose();
    }
}