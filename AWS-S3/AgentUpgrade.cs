using Amazon.DynamoDBv2.DataModel;

namespace AWS_S3;

[DynamoDBTable("AgentUpgradeTable")]
public class AgentUpgrade
{
    [DynamoDBHashKey] // Primary Key
    public string KitPlatform { get; set; }

    [DynamoDBRangeKey] // Sort Key
    public string KitVersion { get; set; }

    [DynamoDBProperty]
    public string Id { get; set; }

    [DynamoDBProperty]
    public string S3ObjectPath { get; set; }

    [DynamoDBProperty]
    public string Sha256Hash { get; set; }

    [DynamoDBProperty]
    public DateTime ReleaseDate { get; set; }
}
