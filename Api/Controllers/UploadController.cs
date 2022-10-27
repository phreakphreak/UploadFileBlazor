using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("upload")]
public class UploadController : ControllerBase
{

    // static readonly string BASE_DIR = Path.GetFullPath(Directory.GetCurrentDirectory());
    static readonly string BASE_DIR = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
    readonly string UPLOAD_DIR = Path.Join(BASE_DIR, "upload");
    readonly string TMP_DIR = Path.Join(BASE_DIR, "tmp");
    readonly List<string> IGNORES = new() { ".DS_STORE" };
    

    [HttpGet]
    [Route("exists")]
    public async Task<IActionResult> Exists([FromQuery] FileDto fileDto)
    {
        if (!Directory.Exists(UPLOAD_DIR))
        {
            Directory.CreateDirectory(UPLOAD_DIR);
        }

        if (!Directory.Exists(TMP_DIR))
        {
            Directory.CreateDirectory(TMP_DIR);
        }
        
        
        var filePath = Path.Combine(UPLOAD_DIR, fileDto!.fileName!);
        var isExists = System.IO.File.Exists(filePath);
        if (isExists)
        {
            return Ok(new ResponseData
            {
                data = new Data
                    { isExists = true, url = "" }
            });
        }

        var chunkIds = new List<string>();
        var chunksPath = Path.Combine(TMP_DIR, fileDto.fileMd5!);
        var hasChunksPath = Directory.Exists(chunksPath);
        if (hasChunksPath)
        {
            chunkIds = await Task.Run(() =>
            {
                var files = Directory.GetFiles(chunksPath);
                return files.Where(file => IGNORES.IndexOf(file) == -1)
                    .Select(file => file.Split("/").Reverse().ToList()[0])
                    .ToList();
            });
        }
        else
        {
            Directory.CreateDirectory(chunksPath);
        }

        return Ok(new ResponseData { data = new Data { isExists = false, chunkIds = chunkIds } });
    }

    [HttpPost]
    [Route("single")]
    public async Task<IActionResult> Upload()
    {
        var file = HttpContext.Request.Form.Files[0];
        var fileInfo = file.FileName.Split("-");
        var filePath = Path.Combine(TMP_DIR, fileInfo[0], fileInfo[1]);
        await using var writeStream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite);
        await file.OpenReadStream().CopyToAsync(writeStream);
        return Ok(new SingleDataResponse { data = file.FileName });
    }

    public async Task CombineMultipleFiles(string sourceDir, string targetPath)
    {
        var files = Directory.GetFiles(sourceDir);
        var result = files.Where(file => IGNORES.IndexOf(file) == -1)
            .Select(file => Convert.ToInt32(file.Split("/").Reverse().ToList()[0]))
            .ToList();
        result!.Sort((a, b) => a.CompareTo(b));
        
        // await using var writeStream = new FileStream(targetPath, FileMode.Create, FileAccess.ReadWrite);
        await using var writeStream = System.IO.File.OpenWrite(targetPath);
        foreach (var filePath in result.Select(file => Path.Combine(sourceDir, file.ToString())))
        {
            var exists = System.IO.File.Exists(filePath);
            if (!exists) throw new Exception("ruta de archivo innacessible: " + filePath);
            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            await fileStream.CopyToAsync(writeStream);
        }
    }

    [HttpGet]
    [Route("concatFiles")]
    public async Task<IActionResult> ConcatFiles([FromQuery] FileDto fileDto)
    {
        try
        {
            var tempDirectoryPath = Environment.GetEnvironmentVariable("TEMP");
            // var filePath = Path.Combine(tempDirectoryPath, Request.Files["file"]);
            // var folder = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var targetPath = String.IsNullOrEmpty(tempDirectoryPath)? Path.Combine(UPLOAD_DIR, fileDto.fileName!): Path.Combine(tempDirectoryPath, fileDto.fileName! );
            
            Console.WriteLine(">>>Target:"+targetPath);
            // var targetPath = Path.Combine(UPLOAD_DIR, fileDto.fileName!);
            var sourceDir = Path.Combine(TMP_DIR, fileDto.fileMd5!);
            
            System.Diagnostics.Debug.WriteLine(targetPath);
            Console.WriteLine(">>>PATH" + Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location));
            await CombineMultipleFiles(sourceDir,targetPath);
            // string RutaURL = await _azureStorageHelper.UploadFile(targetPath, true, "StorageFotoContainer");
            
            
            
            return Ok(new ResponseData
            {
                data = new Data
                {
                    url = targetPath//RutaURL
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine(ex);
            return Ok(ex);
        }
        
    }

    [HttpGet]
    [Route("clean")]
    public async Task<IActionResult> CleanDirectoryTemporal([FromQuery] FileDto fileDto)
    {
        try
        {
            var chunksPath = Path.Combine(TMP_DIR, fileDto.fileMd5!);
            var filePath = Path.Combine(UPLOAD_DIR, fileDto.fileName!);

            if (Directory.Exists(chunksPath))
            {
                var dir = new DirectoryInfo(chunksPath);
                dir.Attributes = dir.Attributes & ~FileAttributes.ReadOnly;
                dir.Delete(true);
            }
            
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            return NotFound("resource not existent");
        }
    }
}

public class Data
{
    public bool isExists { get; set; }
    public string? url { get; set; }
    public List<string>? chunkIds { get; set; }
}

public class SingleDataResponse
{
    public string? data { get; set; }
}

public class ResponseData
{
    public Data? data { get; set; }
}

public class FileDto
{
    [FromQuery(Name = "md5")] public string? fileMd5 { get; set; }

    [FromQuery(Name = "name")] public string? fileName { get; set; }
}