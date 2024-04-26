using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using ModRequestAPI.Models;

namespace ModRequestAPI.Backend.DAL;

public class ModReportingDAL
{
    public static readonly Dictionary<string, string> ModRequestAttributeNames = new Dictionary<string, string>
    {
        {"#MID", "ModID"},
        {"#MN",  "ModName"},
        {"#WC",  "WeaponCount"},
        {"#RC",  "RequestCount"},
        {"#WM",  "WriteMonth"},
    };

    private readonly IAmazonDynamoDB db;

    public ModReportingDAL(IAmazonDynamoDB db)
    {
        this.db = db;
    }

    public async Task<bool> WriteModRequestAsync(IEnumerable<MissingModRequest> requests)
    {
        const string EXPRESSION = @"
            ADD
                #RC :rc
            SET
                #MID = :mid,
                #MN  = :mn,
                #WC  = :wc,
                #WM  = :wm
        ";

        var dt = DateTime.UtcNow;
        string writeMonth = "2" + (dt.Year - 2000) + dt.Month.ToString().PadLeft(2, '0');

        foreach (var request in requests)
        {
            var req = new UpdateItemRequest
            {
                TableName = "mod-reporting",

                Key = new Dictionary<string, AttributeValue>
                {
                    { "pk", new AttributeValue { S = writeMonth + request.ModID } },
                    { "sk", new AttributeValue { S = writeMonth + request.ModID } }
                },

                ExpressionAttributeNames = ModRequestAttributeNames,

                ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                {
                    // Amount to increment request counter by.
                    { ":rc", new AttributeValue { N = "1" } },

                    // Updated mod id.
                    { ":mid", new AttributeValue { S = request.ModID }},

                    // Updated weapon count.
                    { ":wc", new AttributeValue { N = request.WeaponCount.ToString() }},

                    // Updated mod name.
                    { ":mn", new AttributeValue { S = request.ModName }},

                    // Write month used as pk on index for sorting with request count.
                    { ":wm", new AttributeValue { N =  writeMonth }}
                },

                UpdateExpression = EXPRESSION,
            };

            var response = await db.UpdateItemAsync(req);
            if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
                return false;
        }

        return true;
    }
}
