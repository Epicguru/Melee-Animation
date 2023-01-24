using ModRequestAPI;
using Xunit;

namespace ModRequestAPITests
{
    public class UpdateGist
    {
        private const string GIST_ID = "252c256aea7ad36dfaa6cda4d9b40f11";

        [Fact]
        public async Task UpdateGistTest()
        {
            var client = new ModRequestClient(GIST_ID);

            // Get existing. There should only be 1.
            Dictionary<string, ModData> data = new Dictionary<string, ModData>();
            foreach (var pair in await client.GetModRequests())
            {
                data.Add(pair.modID, pair.data);
            }
            Assert.Single(data);
            
            int oldCount = data.Values.First().RequestCount;
            string id = data.Keys.First();
            string fileID = ModRequestClient.GetFileName(id);

            IEnumerable<string> GetIdsToUpdate()
            {
                yield return id;
            }

            bool UpdateAction(string _, ModData d)
            {
                d.RequestCount += 12;
                return true;
            }

            foreach (var update in await client.UpdateModRequests(GetIdsToUpdate(), "Unit Test Update", UpdateAction))
            {
                Assert.Equal(fileID, ModRequestClient.GetFileName(update.modID));
                Assert.Equal(oldCount + 12, update.data.RequestCount);
            }
        }

        [Fact]
        public async Task AddRangeTest()
        {
            var client = new ModRequestClient(GIST_ID);

            var rand = new Random();
            const int TO_ADD = 10_000;

            List<string> ids = new List<string>();
            for (int i = 0; i < TO_ADD; i++)
            {
                ids.Add($"ID{Guid.NewGuid()}_{i}");
            }

            bool AddNew(string _, ModData data)
            {
                data.RequestCount = rand.Next(10);
                return true;
            }

            bool Delete(string _1, ModData _2) => false;

            await client.UpdateModRequests(ids, "Unit Test Mass Update", AddNew);

            int count = 0;
            foreach (var _ in await client.GetModRequests())
            {
                count++;
            }

            Assert.Equal(TO_ADD + 1, count);

            await client.UpdateModRequests(ids, "Unit Test Mass Delete", Delete);

            count = 0;
            foreach (var _ in await client.GetModRequests())
            {
                count++;
            }

            Assert.Equal(1, count);
        }
    }
}