using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Services.Gateway;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Repository to load item conditions from dbo.Condition.
    /// </summary>
    public class ItemConditionRepository
    {
        public IList<ConditionDto> GetAll()
        {
            if (GatewayClient.UseForData)
                return GatewayClient.Get<List<ConditionDto>>("api/conditions") ?? new List<ConditionDto>();

            var results = new List<ConditionDto>();
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            using (var cmd = new SqlCommand(@"
                SELECT ConditionId, ConditionName
                FROM dbo.[Condition]
                ORDER BY ConditionId", con))
            {
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        results.Add(new ConditionDto
                        {
                            ConditionId = reader.GetInt32(0),
                            ConditionName = reader.GetString(1),
                            SortOrder = reader.GetInt32(0),
                            IsActive = true
                        });
                    }
                }
            }
            return results;
        }
    }
}
