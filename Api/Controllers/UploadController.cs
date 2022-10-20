using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("upload")]
public class UploadController : ControllerBase
{
    static readonly string BASE_DIR = Path.GetFullPath(Directory.GetCurrentDirectory());
    readonly string UPLOAD_DIR = Path.Combine(BASE_DIR, "wwwroot", "upload");
    readonly string TMP_DIR = Path.Combine(BASE_DIR, "tmp");
    readonly List<string> IGNORES = new() { ".DS_STORE" };

    [HttpGet]
    [Route("exists")]
    public async Task<IActionResult> Exists([FromQuery] FileDto fileDto)
    {
        string filePath = Path.Combine(UPLOAD_DIR, fileDto!.fileName!);
        bool isExists = System.IO.File.Exists(filePath);
        if (isExists)
        {
            return Ok(new ResponseData { data = new Data { isExists = true, url = $"{Request.Scheme}://{Request.Host}/public/{fileDto.fileName!}" } });
        }
        else
        {
            var chunkIds = new List<string>();
            var chunksPath = Path.Combine(TMP_DIR, fileDto.fileMd5!);
            var hasChunksPath = Directory.Exists(chunksPath);
            Console.WriteLine(">>> has chunk: " + hasChunksPath.ToString());

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
            return Ok(new ResponseData { data = new Data { isExists = false, chunkIds = new List<string>() } });
        }
    }
    private byte[] GetBytes(Stream input)
    {
        byte[] buffer = new byte[input.Length];
        using (MemoryStream ms = new MemoryStream())
        {
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                ms.Write(buffer, 0, read);
            }

            return ms.ToArray();
        }
    }
    public static void SaveStreamAsFile(string filePath, Stream inputStream, string fileName)
    {
        DirectoryInfo info = new DirectoryInfo(filePath);
        if (!info.Exists)
        {
            info.Create();
        }

        string path = Path.Combine(filePath, fileName);
        using (FileStream outputFileStream = new FileStream(path, FileMode.Create))
        {
            inputStream.CopyTo(outputFileStream);
        }
    }
    [HttpPost]
    [Route("single")]
    public async Task<IActionResult> Upload()
    {
        var file = HttpContext.Request.Form.Files[0];
        var fileInfo = file.FileName.Split("-");
        var filePath = Path.Combine(TMP_DIR, fileInfo[0], fileInfo[1]);

        using var writeStream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite);

        // using var fileStream = new FileStream(file.FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
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

        using var writeStream = new FileStream(targetPath, FileMode.Create, FileAccess.ReadWrite);
        foreach (var file in result)
        {
            var filePath = Path.Combine(sourceDir, file.ToString());

            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            await fileStream.CopyToAsync(writeStream);
        }
    }

    [HttpGet]
    [Route("concatFiles")]
    public async Task<IActionResult> ConcatFiles([FromQuery] FileDto fileDto)
    {
        await CombineMultipleFiles(
            Path.Combine(TMP_DIR, fileDto.fileMd5!),
            Path.Combine(UPLOAD_DIR, fileDto.fileName!)
        );

        return Ok(new ResponseData
        {
            data = new Data
            {
                url = $"{Request.Scheme}://{Request.Host}/public/{fileDto.fileName!}"
            }
        });
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
    [FromQuery(Name = "md5")]
    public string? fileMd5 { get; set; }

    [FromQuery(Name = "name")]
    public string? fileName { get; set; }
}
