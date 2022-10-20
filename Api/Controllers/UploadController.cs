using Microsoft.AspNetCore.Mvc;


namespace Api.Controllers;

[ApiController]
[Route("upload")]
public class UploadController : ControllerBase
{
    static string BASE_DIR = Path.GetFullPath(System.IO.Directory.GetCurrentDirectory());
    string UPLOAD_DIR = Path.Combine(BASE_DIR, "public", "upload");
    string TMP_DIR = Path.Combine(BASE_DIR, "tmp");
    List<string> IGNORES = new List<string> { ".DS_STORE" };

    [HttpGet]
    [Route("exists")]
    public async Task<IActionResult> Exists([FromQuery] FileDto fileDto)
    {

        string filePath = Path.Combine(UPLOAD_DIR, fileDto!.fileName!);
        bool isExists = System.IO.File.Exists(filePath);
        if (isExists)
        {
            return Ok(new Data { isExists = true, url = $"{Request.Scheme}://{Request.Host}/public/{fileDto.fileName!}" });
        }
        else
        {
            var chunksIds = new List<string>();
            var chunksPath = Path.Combine(TMP_DIR, fileDto.fileMd5!);
            Directory.CreateDirectory(chunksPath);
            var hasChunksPath = Directory.Exists(chunksPath);
            Console.WriteLine(">>> has chunk: " + hasChunksPath.ToString());

            if (hasChunksPath)
            {
                chunksIds = await Task.Run<List<string>>(() =>
                            {
                                var files = Directory.GetFiles(chunksPath);
                                return files.Where(file => IGNORES.IndexOf(file) == -1)
                                            .Select(file => file.Split("/").Reverse().ToList()[0])
                                            .ToList();
                            });
            }
            return Ok(new Data { isExists = false, chunksIds = chunksIds });
        }
    }

    [HttpPost]
    [Route("upload/single")]
    public async Task<IActionResult> Upload(List<IFormFile> files)
    {
        Console.WriteLine(">>> Request" + Request.ToString());
        return Ok("ok?");
    }

    public async Task CombineMultipleFiles(string sourceDir, string targetPath)
    {
        var files = Directory.GetFiles(sourceDir);
        var result = files.Where(file => IGNORES.IndexOf(file) == -1)
                        .Select(file => Convert.ToInt32(file.Split("/").Reverse().ToList()[0]))
                        .ToList();
        result!.Sort((a, b) => a.CompareTo(b));

        using (var writeStream = new FileStream(targetPath, FileMode.Create, FileAccess.ReadWrite))
        {
            foreach (var file in result)
            {
                var filePath = Path.Combine(sourceDir, file.ToString());

                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    await fileStream.CopyToAsync(writeStream);
                }

            }
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

        return Ok(new Data
        {
            url = $"{Request.Scheme}://{Request.Host}/public/{fileDto.fileName!}"
        });
    }
}




public class Data
{
    public bool isExists { get; set; }
    public string? url { get; set; }
    public List<string>? chunksIds { get; set; }
}

public class Response
{
    public string? status { get; set; }
    public Data? data { get; set; }
}

public class FileDto
{

    [FromQuery(Name = "md5")]
    public string? fileMd5 { get; set; }

    [FromQuery(Name = "name")]
    public string? fileName { get; set; }
}
