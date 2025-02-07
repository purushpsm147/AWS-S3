using Amazon.CloudFront;
using Amazon.DynamoDBv2.DataModel;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.AspNetCore.Mvc;

namespace AWS_S3.Controllers;

[ApiController]
[Route("[controller]")]
public class AgentUpgradeController(IAmazonS3 s3Client,
    IDynamoDBContext dynamoDBContext,
    IAmazonSecretsManager amazonSecretsManager,
    ILogger<AgentUpgradeController> logger) : ControllerBase
{

    private const string BucketName = "";
    private const string DistributionUrl = "";
    private const string KeyPairId = "";
    private const string SecretName = "";

    [HttpGet("getMultipleFilesPreSignedURLFromS3")]
    public async Task<IActionResult> GetFiles(string prefix)
    {

        var request = new ListObjectsV2Request()
        {
            BucketName = BucketName,
            Prefix = prefix
        };

        var response = await s3Client.ListObjectsV2Async(request);
        var presignedUrls = response.S3Objects.Select( o => GetPreSignedUrlS3(s3Client, o));
        return Ok(presignedUrls);

    }



    [HttpGet("SignedURLFromCloudFront")]
    public async Task<IActionResult> GetSignedURL(string filename)
    {
        //resource url is the cloudfront url
        var distributionUrl = "https://d24r6l459kdfw3.cloudfront.net";
        var resourceUrl = distributionUrl + "/" + filename;
        string finalUrl = await GetCloudFrontPreSignedUrl(distributionUrl, resourceUrl);
        return Ok(finalUrl);
    }

    

    [HttpGet("listBuckets")]
    public async Task<IActionResult> ListAsync()
    {
        var data = await s3Client.ListBucketsAsync();
        var buckets = data.Buckets.Select(b => b.BucketName).Take(4);
        return Ok(buckets);
    }

    [HttpGet("getFileOnS3", Name = "Get File on S3")]
    public async Task<IActionResult> GetFile(string fileName)
    {
        var s3Object = new S3Object()
        {
            Key = fileName
        };
        var presignedUrl = GetPreSignedUrlS3(s3Client, s3Object);
        return Ok(presignedUrl);
    }

    [HttpGet("api/v1/agent-upgrade/upgrades/latest", Name = "Get Latest Agent Kit PreSigned URL")]
    public async Task<IActionResult> GetLatestAgentKit()
    {
        // Fetch the latest agent kit from DynamoDB
        var latestAgentKit = await dynamoDBContext.LoadAsync<AgentUpgrade>("Windows", "1.0.0");

        // Extract the file name from the S3ObjectPath
        var resourceUrl = new Uri(latestAgentKit.S3ObjectPath);
        var fileName = resourceUrl.Segments.Last();

        // Generate a presigned URL from CloudFront
        var cloudFrontUrl = DistributionUrl + "/" + fileName;
        string finalUrl = await GetCloudFrontPreSignedUrl(DistributionUrl, cloudFrontUrl);

        return Ok(finalUrl);
    }

    [HttpPost("UploadFilesToS3")]
    public async Task Post(IFormFile formFile)
    {
        var bucketRequest = new PutBucketRequest()
        {
            BucketName = BucketName,
            UseClientRegion = true,
        };

        var bucketExists = await AmazonS3Util.DoesS3BucketExistV2Async(s3Client, BucketName);
        if (!bucketExists)
        {
            await s3Client.PutBucketAsync(bucketRequest);
        }
        var objectResult = new PutObjectRequest()
        {
            BucketName = BucketName,
            Key = $"{DateTime.Now:yyyy\\/MM\\/dd\\/hhmmss}-{formFile.FileName}",
            InputStream = formFile.OpenReadStream(),
            StorageClass = S3StorageClass.IntelligentTiering
        };

        objectResult.Metadata.Add("Testing", "MetaData");
        var response = await s3Client.PutObjectAsync(objectResult);
        
    }


    [HttpDelete("Delete Bucket")]
    public async Task Delete()
    {
        await s3Client.DeleteBucketAsync(BucketName);
    }

    [HttpDelete("Delete File")]
    public async Task DeleteFile(string fileName)
    {

        await s3Client.DeleteObjectAsync(BucketName, fileName);
    }

    private async Task<string> GetCloudFrontPreSignedUrl(string distributionUrl, string resourceUrl)
    {
        string policyDoc = AmazonCloudFrontUrlSigner.BuildPolicyForSignedUrl(
        resourceUrl,
        DateTime.Now.AddHours(2), // valid for 2 hours
        null);
        var secret = await GetSecret(SecretName);
        var privateKey = new StringReader(secret);


        //
        string signurl = AmazonCloudFrontUrlSigner.SignUrl(
            resourceUrl,
            KeyPairId,
            privateKey,
            policyDoc);


        Uri baseUri = new Uri(distributionUrl);
        Uri combinedUri = new Uri(baseUri, signurl);

        string finalUrl = combinedUri.ToString();
        return finalUrl;
    }
    private string GetPreSignedUrlS3(IAmazonS3 s3Client, S3Object s3Object)
    {
        var request = new GetPreSignedUrlRequest()
        {
            BucketName = BucketName,
            Key = s3Object.Key,
            Expires = DateTime.Now.AddHours(2)
        };
        return s3Client.GetPreSignedURL(request);
    }

    private async Task<string> GetSecret(string secretName)
    {
        var request = new GetSecretValueRequest()
        {
            SecretId = secretName,
            VersionStage = "AWSCURRENT"
        };

        GetSecretValueResponse response;
        try
        {
            response = await amazonSecretsManager.GetSecretValueAsync(request);
        }
        catch (Exception e)
        {

            throw e;
        }

        return response.SecretString;
    }
}
