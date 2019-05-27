# Exercise 4 - Bringing it all together

## Making our application do something

We have our own custom application and it is now building as a Docker image. However, the application currently does very little - it simple outputs a line to the console. One of the key features of Docker Compose is the ability to bring up several services all at once that need to communicate with each other. We've set up our phpMyAdmin container so that it can communicate with the database container, but our console application is not making use of any of this, nor has it been incorporated into our stack - it currently site alone as its own Docker image.

First of all, let's change our application so that it makes use of the database. In your test-app directory, replace the contents of `Program.cs` and `test-app.csproj` with the following:

**Program.cs**

```csharp
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
```

**test-app.csproj**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp2.2</TargetFramework>
    <RootNamespace>test_app</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MySQL.Data" Version="8.0.16" />
  </ItemGroup>

</Project>
```

The contents of this app aren't super important for the purposes of this exercise, but there are a couple of things we should take note of. In the first few lines the application attempts to gather some configuration from environment variables. You'll notice that some are set with sensible defaults if not present in the environment variables, but others will cause the application to exit if they are not provided so we will need to ensure we configure those environment variables in our `docker-compose.yml`.

Secondly, we have a loop that attempts to connect to the database for 30 seconds before giving up. This is because when we first start up our database service it will perform all its initialisation steps. This can take some time, so we want to give it at least 30 seconds to come up before we finally give up on it.

## Adding your image to the stack

We now have an application that wants to use our database, so let's return to our `docker-compose.yml` file from Exercise 2 and add our application to the stack.

```yaml
version: "3.7"

services:
  database:
    image: mysql:latest
    command: --default-authentication-plugin=mysql_native_password
    environment:
      MYSQL_DATABASE: mydb
      MYSQL_ROOT_PASSWORD: password
  phpmyadmin:
    image: phpmyadmin/phpmyadmin:latest
    ports:
      - 1001:80
    environment:
      PMA_HOST: database
  test-app:
    depends_on:
      - database
    build:
      context: ./test-app
      dockerfile: Dockerfile
    environment:
      MYSQL_HOST: database
      MYSQL_PASSWORD: password
      MYSQL_DB: mydb
```

There's quite a bit to unpack here, so let's start with the extra environment variable we've added to our `database` container. We've added a new variable called `MYSQL_DATABASE` with the value `mydb`. This simply instructs MySQL to create this database on initialisation, so we can be assured that it will be there from the get-go.

The most obvious change is the addition of an entirely new service called `test-app`. You'll notice that this app doesn't specify an image name: this is because we will be building our own image rather than using a pre-built one. Instead, you can see in the `build` node we have a `dockerfile` property that tells Docker Compose that we should use that file to build the image rather than fetching an existing one. Also under the `build` node is the `context` property. This is the same functionality as we explored in Exercise 3, where we provide Docker with a build context so that Docker knows which files are needed to perform the build.

**Note:** The location of the Dockerfile is relative to the build context. Even though our Dockerfile is actually in a `test-app` directory, we do not need to include this in the path because the `test-app` directory is actually the root folder of the build context.

We've also configured the `depends_on` property. This tells Docker Compose that we should start the database container first, and then when that is up and running we can start the `test-app` container.

Finally, we set our environment variables. As before, our database can be accessed using the hostname `database` and for the password and database values we just pass in those values we've already configured in MySQL.

## Firing it up

We're all done! We finally have our entire stack ready to go. First, let's make sure we reset everything back to the start by running this command in the same directory as `docker-compose.yml`:

```
docker-compose down
```

With this done, we're ready to fire it up:

```
docker-compose up --build
```

The `--build` command is simply to tell Docker Compose that it should build our custom image each time. You can instead manually build the image and then omit `--build`, but this is a little more convenient as it combines the two steps into one.

You should see quite a bit of output as the database server and the phpMyAdmin server start up and initialise themselves. In amongst this you should see our console app attempting to connect to the database. Finally, once everything is initialised the console app should be able to connect and perform its tasks. It will create a new table called books, if one doesn't exist, add four records to the table and then query the table for those records and output the results.

The result in the console should look something like this:

```
test-app_1    | Creating table if it doesn't already exist... DONE
test-app_1    | Inserting seed records...
test-app_1    | - Inserting record... DONE
test-app_1    | - Inserting record... DONE
test-app_1    | - Inserting record... DONE
test-app_1    | - Inserting record... DONE
test-app_1    | DONE
test-app_1    | Retrieving books... DONE
test-app_1    | ---------------------------------
test-app_1    | - 1: Book 1
test-app_1    | - 2: Book 2
test-app_1    | - 3: Book 3
test-app_1    | - 4: Book 4
complete_test-app_1 exited with code 0
```

And that's it! We've incorporated two pre-built images and our own custom built image into a complete stack that can now be spun up and shut down together with ease.
