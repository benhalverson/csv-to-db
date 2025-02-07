using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Dapper;
using MySqlConnector;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Azure.Cosmos;

namespace cli_dotnet
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: dotnet run --db=<mysql|cosmos> --data=<path-to-csv>");
                return;
            }

            // Parse arguments
            string dbType = GetArgumentValue(args, "--db");
            string csvFilePath = GetArgumentValue(args, "--data");

            if (string.IsNullOrEmpty(dbType) || string.IsNullOrEmpty(csvFilePath))
            {
                Console.WriteLine("Error: Missing required parameters.");
                return;
            }

            if (!File.Exists(csvFilePath))
            {
                Console.WriteLine($"Error: CSV file not found at {csvFilePath}");
                return;
            }

            IConfiguration config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Process based on DB selection
            if (dbType.ToLower() == "mysql")
            {
                string mysqlConnectionString = config.GetSection("MySQL:ConnectionString").Value
                    ?? throw new Exception("MySQL connection string not found!");

                await using var mysqlConnection = new MySqlConnection(mysqlConnectionString);
                await mysqlConnection.OpenAsync();

                Console.WriteLine("Connected to MySQL successfully.");
                await EnsureMySQLSchemaExists(mysqlConnection);
                await InsertUsersIntoMySQL(mysqlConnection, csvFilePath);
            }
            else if (dbType.ToLower() == "cosmos")
            {
                string cosmosEndpoint = config.GetSection("CosmosDB:Endpoint").Value
                    ?? throw new Exception("CosmosDB Endpoint not found!");
                string cosmosKey = config.GetSection("CosmosDB:Key").Value
                    ?? throw new Exception("CosmosDB Key not found!");
                string cosmosDatabase = config.GetSection("CosmosDB:Database").Value
                    ?? throw new Exception("CosmosDB Database name not found!");
                string cosmosContainer = config.GetSection("CosmosDB:Container").Value
                    ?? throw new Exception("CosmosDB Container name not found!");

                using CosmosClient cosmosClient = new CosmosClient(cosmosEndpoint, cosmosKey);
                var database = await cosmosClient.CreateDatabaseIfNotExistsAsync(cosmosDatabase);
                var container = await database.Database.CreateContainerIfNotExistsAsync(cosmosContainer, "/id");

                Console.WriteLine("Connected to Azure Cosmos DB successfully.");
                await InsertUsersIntoCosmosDB(container.Container, csvFilePath);
            }
            else
            {
                Console.WriteLine("Error: Unsupported database type. Use 'mysql' or 'cosmos'.");
            }
        }

        /// <summary>
        /// Ensures the MySQL users table exists.
        /// </summary>
        private static async Task EnsureMySQLSchemaExists(MySqlConnection connection)
        {
            string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS users (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    name VARCHAR(100) NOT NULL,
                    email VARCHAR(255) UNIQUE NOT NULL,
                    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                );
            ";
            await connection.ExecuteAsync(createTableQuery);
        }

        /// <summary>
        /// Reads a CSV file and inserts users into MySQL.
        /// </summary>
        private static async Task InsertUsersIntoMySQL(MySqlConnection connection, string csvFilePath)
        {
            using var reader = new StreamReader(csvFilePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));

            var users = csv.GetRecords<User>().ToList();

            string insertQuery = @"
                INSERT INTO users (name, email, created_at)
                VALUES (@Name, @Email, @CreatedAt)
                ON DUPLICATE KEY UPDATE name = VALUES(name), created_at = VALUES(created_at);
            ";

            await connection.ExecuteAsync(insertQuery, users);
            Console.WriteLine("Data inserted into MySQL successfully.");
        }

        /// <summary>
        /// Reads a CSV file and inserts users into CosmosDB.
        /// </summary>
        private static async Task InsertUsersIntoCosmosDB(Container container, string csvFilePath)
        {
            using var reader = new StreamReader(csvFilePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));

            var users = csv.GetRecords<User>().ToList();
            foreach (var user in users)
            {
                await container.UpsertItemAsync(user, new PartitionKey(user.Id.ToString()));
            }

            Console.WriteLine("Data inserted into CosmosDB successfully.");
        }

        /// <summary>
        /// Parses command-line arguments.
        /// </summary>
        private static string GetArgumentValue(string[] args, string key)
        {
            var arg = args.FirstOrDefault(a => a.StartsWith(key + "="));
            return arg?.Split('=')[1];
        }
    }

    /// <summary>
    /// Represents a User object.
    /// </summary>
    public class User
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public required string Name { get; set; }
        public required string Email { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // CsvHelper mapping for CSV columns
    public class UserMap : ClassMap<User>
    {
        public UserMap()
        {
            Map(m => m.Name).Name("Name");
            Map(m => m.Email).Name("Email");
            Map(m => m.CreatedAt).Name("CreatedAt");
        }
    }
}
