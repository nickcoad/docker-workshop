# Exercise 3 - Building an image

## Creating a Dockerfile

Up until this point we've been using Docker images that other people have prepared and hosted on the Docker registry. If we're building our own application however, we won't have access to a premade image, we'll need to build one ourselves. This is also done with the standard Docker toolkit and doesn't require any additional tools, so we're already ready to go.

Much like Docker Compose uses a `docker-compose.yml` file for its configuration, a Docker image will use a `Dockerfile` for its configuration. This file tells Docker how it should build the image. Once the image has been built, Docker will automatically add the image to your machine's local registry and the image can then be used just as you would use any other image.

In order to demonstrate this, we'll create a very simple C# console application that connects to our database and outputs some data.

To start, we'll create the C# application. Inside the directory you placed your `docker-compose.yml` file into, create a new directory called test-app. In this directory create the following two files:

**Program.cs**

```csharp
using System;

namespace test_app
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello from my container!");
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
e
</Project>
```

At this point, you might be rushing off to download the .NET Core SDK so that you can build this application, but you don't actually need to - we're going to build this application _inside_ of a container and then publish it to a final image. If this sounds confusing, don't worry, it'll make sense soon!

We have our basic application, now we need to build the application and create an image that can host it. We'll start by building the application using the .NET Core SDK within a container. Create a file called `Dockerfile` inside your `test-app` directory and populate it with the following:

```dockerfile
FROM mcr.microsoft.com/dotnet/core/sdk:2.2
```

This tells Docker that we want to start our build using the `mcr.microsoft.com/dotnet/core/sdk:2.2` image as starting point. This will download the image, which contains the .NET Core SDK and allow us to use it to build our application. Now we need to store the build context somewhere (more on this soon) and then set our working directory to that path:

```dockerfile
FROM mcr.microsoft.com/dotnet/core/sdk:2.2
COPY . /tmp/build
WORKDIR /tmp/build
```

This tells the container to copy all the files from the build context into the `/tmp/build` directory.

Now we want to start to build our application, the first command we need to run is `dotnet restore`, which will restore all the packages for our application. We want this to run inside the SDK container, so we place it after the command we used to retrieve the SDK image:

```dockerfile
FROM mcr.microsoft.com/dotnet/core/sdk:2.2
COPY . /tmp/build
WORKDIR /tmp/build
RUN dotnet restore
```

Finally we want to run `dotnet publish` to build the application ready for running in a container.

```dockerfile
FROM mcr.microsoft.com/dotnet/core/sdk:2.2
COPY . /tmp/build
WORKDIR /tmp/build
RUN dotnet restore
RUN dotnet publish
```

Let's give this a try and have a look at what happens. To build an image using this configuration, we use the `docker build` command.

```
docker build .
```

It's very important to note the period at the end of the command. The `docker build` command expects you to tell it where to find its "build context". The build context is just a set of files that should be present in order to build the project. In our case, we're just setting up everything in the current directory (that's what the period means) to be used as the build context. This context is placed in memory and then copied into the container using the `COPY . /tmp/build` command. This copies everything from the root of the build context to the directory `/tmp/build` on the container. We then set our working directory to that path.

This means by the time we run `dotnet restore` and `dotnet publish` we are sitting in a directory containing our application source code and project file.

Now lets take a look at the output from our build process. Notice we have 5 lines in our Dockerfile and in the build output, we have Step 1/5, Step 2/5 and so on. Each line is referred to as a step, and the ouput of each step will be neatly displayed underneath. Using this information, we can see that the publish command worked as expected, and output the result to the usual directory. We now need to take that result and use it to build an image that can host the published application. This is where we introduce what's called 'build stages'.

## Adding a new build stage

Our first build stage is already done. It takes in the project files, builds them and produces some output. We now want to host that output in a container capable of understanding how to run .NET Core applications. We could use the .NET Core SDK image for this, however since we only want to _host_ the application, we would be including a whole lot of unnecessary overhead if we hosted it using the entire SDK, so we'll host it using the .NET Core runtime image instead. This means we have two distinct stages, one that uses the .NET Core SDK image to perform the application build, and another that uses the .NET Core runtime to host the built application.

In our Dockerfile, we start the new stage by specifying a new base image to work with:

```dockerfile
FROM mcr.microsoft.com/dotnet/core/sdk:2.2
COPY . /tmp/build
WORKDIR /tmp/build
RUN dotnet restore
RUN dotnet publish

FROM mcr.microsoft.com/dotnet/core/runtime:2.2
```

Our final built image will now be a copy of the .NET Core runtime image, the result of the first 5 steps is simply thrown away. Instead, let's take the results of the first 5 steps and copy them into the second image so we can host the application there. The first step to doing this is to give the first built image an alias, this way we can copy files from that image using its alias.

```dockerfile
FROM mcr.microsoft.com/dotnet/core/sdk:2.2 AS build
COPY . /tmp/build
WORKDIR /tmp/build
RUN dotnet restore
RUN dotnet publish -o output

FROM mcr.microsoft.com/dotnet/core/runtime:2.2 AS runtime
WORKDIR /app
COPY --from=build /tmp/build/output /app
```

We've made quite a few changes here, so let's explore them:

1. On line 1, we allocated an alias of `build` to the output of our first build stage using the `AS` keyword.
2. On line 5, we made a small adjustment to force the output of our publish command into a simpler directory name for convenience.
3. On line 7, we allocated an alias of `runtime` to the output of our second build stage using the `AS` keyword.
4. On line 8, we set our working directory to `/app`. It's worth noting that this will create the directory if it doesn't exist, you can set this to whatever directory you like.
5. Finally we perform a copy as normal, except we tell Docker that we'd like to use the `build` stage as the source filesystem for the copy operation.

So we now have a .NET Core runtime image with our application's published code copied onto it. We now need to tell the image how to start the application when the image is started up in a container.

```dockerfile
FROM mcr.microsoft.com/dotnet/core/sdk:2.2 AS build
COPY . /tmp/build
WORKDIR /tmp/build
RUN dotnet restore
RUN dotnet publish -o output

FROM mcr.microsoft.com/dotnet/core/runtime:2.2 AS runtime
WORKDIR /app
COPY --from=build /tmp/build/output /app
ENTRYPOINT ["dotnet", "test-app.dll"]
```

`ENTRYPOINT` is a step that we can use to tell Docker what to do with the container when it first starts up.

We now have everything we need! Let's build this image and try to start it up in a container.

```
docker build -t test-app .
docker run test-app
```

Notice we made a very small change to the build command - we've given the image a tag so that we can more easily target it with the `docker start` command on the next line.

If everything has gone according to plan, you should receive the following output:

```
Hello from my container!
```

Congratulations! Those images we've used in the previous exercises? You've now built your _very own_ image with your very own application on it. If you wanted to you could publish this image to the Docker registry and anyone could re-use it, or you could set up your own image registry and your team will all be able to access the images. Services to host your own Docker image registries are provided by AWS and Azure DevOps, among others.

Additionally, we've experienced one of the key value-adds of Docker - we were able to compile and publish a .NET Core application without downloading or installing any .NET Core tooling to our machines, it was all done entirely within Docker containers! If this were in a Git repository, a new developer could clone the repository, modify the application and build it without ever installing a single .NET Core tool.
