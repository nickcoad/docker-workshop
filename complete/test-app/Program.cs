using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace test_app
{
    class Program
    {
        /// <summary>
        /// Maximum time to wait for a database connection 
        /// </summary>
        private const int MaximumDbWaitTime = 30000;

        private enum ExitCodes
        {
            Success = 0,
            Error = 1
        }

        static async Task<int> Main(string[] args)
        {
            var mysqlHost = Environment.GetEnvironmentVariable("MYSQL_HOST") ?? "localhost";
            var mysqlPort = Environment.GetEnvironmentVariable("MYSQL_PORT") ?? "3306";
            var mysqlUsername = Environment.GetEnvironmentVariable("MYSQL_USERNAME") ?? "root";
            var mysqlPassword = Environment.GetEnvironmentVariable("MYSQL_PASSWORD");
            var mysqlDatabase = Environment.GetEnvironmentVariable("MYSQL_DB");

            var error = false;
            if (mysqlPassword == null)
            {
                error = true;
                Console.WriteLine("Required environment variable was not provided: MYSQL_PASSWORD.");

            }
            if (mysqlDatabase == null)
            {
                error = true;
                Console.WriteLine("Required environment variable was not provided: MYSQL_DB.");
            }

            if (error)
                return (int)ExitCodes.Error;

            var connectionString = $"server={mysqlHost};user={mysqlUsername};database={mysqlDatabase};port={mysqlPort};password={mysqlPassword}";

            var books = new List<string>();
            using (var conn = new MySqlConnection(connectionString))
            {
                var sw = new Stopwatch();
                sw.Start();

                var connected = false;

                while (connected == false)
                {
                    Console.WriteLine("Waiting for db... ");
                    try
                    {
                        await conn.OpenAsync();
                        connected = true;
                    }
                    catch
                    {
                        if (sw.ElapsedMilliseconds >= MaximumDbWaitTime)
                        {
                            Console.WriteLine("A connection to the database could not be established.");
                            return (int)ExitCodes.Error;
                        }

                        Thread.Sleep(2000);
                    }
                }

                await CreateTable(conn);
                await SeedRows(conn);

                books = await RetrieveRows(conn);
            }

            Console.WriteLine("---------------------------------");
            foreach (var book in books)
            {
                Console.WriteLine($"- {book}");
            }

            return (int)ExitCodes.Success;
        }

        private static async Task CreateTable(MySqlConnection conn)
        {
            Console.Write("Creating table if it doesn't already exist... ");
            var createTableSql = "CREATE TABLE IF NOT EXISTS books (id INT AUTO_INCREMENT, title VARCHAR(20) NOT NULL DEFAULT '', PRIMARY KEY (id))";
            var command = new MySqlCommand(createTableSql, conn);
            await command.ExecuteNonQueryAsync();
            Console.WriteLine("DONE");
        }

        private static async Task SeedRows(MySqlConnection conn)
        {
            Console.WriteLine("Inserting seed records... ");
            var insertSqls = new[]
            {
                "REPLACE INTO books (id, title) VALUES (1, 'Book 1')",
                "REPLACE INTO books (id, title) VALUES (2, 'Book 2')",
                "REPLACE INTO books (id, title) VALUES (3, 'Book 3')",
                "REPLACE INTO books (id, title) VALUES (4, 'Book 4')",
            };

            foreach (var sql in insertSqls)
            {
                Console.Write("- Inserting record... ");
                var command = new MySqlCommand(sql, conn);
                await command.ExecuteNonQueryAsync();
                Console.WriteLine("DONE");
            }
            Console.WriteLine("DONE");
        }

        private static async Task<List<string>> RetrieveRows(MySqlConnection conn)
        {
            Console.Write("Retrieving books... ");
            var sql = "SELECT * FROM books";

            var command = new MySqlCommand(sql, conn);
            var reader = await command.ExecuteReaderAsync();
            var results = new List<string>();

            while (await reader.ReadAsync())
            {
                results.Add($"{reader[0]}: {reader[1]}");
            }

            Console.WriteLine("DONE");
            return results;
        }
    }
}
