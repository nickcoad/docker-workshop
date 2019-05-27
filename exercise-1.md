# Exercise 1 - Getting started

## Running your first container

For this exercise, we will walk through the process of retrieving a Docker image from Docker's own image registry and running that image inside a container. The image we'll use will be a MySQL database server. We will then validate that the server is running using docker commands to attach to the container as well as list all running containers.

Docker images are like 'snapshots' of a virtual machine. Many images are Linux-based and essentially consist of a lightweight Linux distro of some sort with whatever specific service you want pre-installed on the system.

For example, the MySQL image we will be using contains an install of Debian version 9 (stretch), with MySQL pre-installed. The image is then configured to start MySQL up automatically when the container is started. The installation of MySQL is also configured to draw settings from environment variables, allowing you to pass in configuration options at the time that you start up the container.

### Retrieving the MySQL Docker image

The first step in getting all this up and running is to retrieve the MySQL Docker image from Docker's image registry. The command for this is simple:

```
docker pull mysql
```

When you run this command a few things are occuring behind the scenes. First, Docker will attempt to work out which image registry you're trying to pull the image from. By default, this is the Docker registry, so we don't need to provide any special configuration in order for Docker to work this out. Docker will then check the registry to see whether an image with that name exists. Of course, there is an image in Docker's registry with the name `mysql` so we're all good.

The next step can be a bit confusing until you understand what's happening, and can sometimes trip you up if you are pulling an image with a non-standard versioning pattern. Docker will try to identify what _version_ of the image to pull down. Versioning of Docker images is done using 'tags'. By default, if you don't ask for a specific version, Docker will look for the version that has been tagged 'latest'. Occasionally you may encounter an image that doesn't have a latest tag, or you might want to specify an older version of the image. In this case, you could type something like this:

```
docker pull mysql:8.0
```

This would tell Docker not to assume which version we want, and to instead pull the specific version we've asked for - that is, the version of the image that has been 'tagged' as 8.0. Remember that these tags are at the image developers discretion so you can't always be sure a specific tag (like 'latest') will be available. Unfortunately there is no command to check which tags are available, however you can browse to the images page on Docker hub (i.e. https://hub.docker.com/_/mysql) and usually the tags will be listed on the main page. If not, there is a tags tab that lists all available tags.

For the purpose of this exercise, we'll stick with the version we've already pulled: `mysql:latest`.

Finally, once all of the above details have been worked out by Docker, it starts downloading the image from the registry to your machine. When the image has finished downloading the SHA256 hash of the image is displayed and the image is ready to use!

### Running the MySQL Docker container

Now that we have an image available to us, the next step is to tell Docker to start up a new container using that image, and this will be the actual instance of the server that other services can connect to. This is done using the `docker run` command (don't expect this to work just yet, but run the command so you can see the error that is generated).

```
docker run --name docker-workshop-mysql mysql:latest
```

In order to make some future commands easier to use, we have given the container a name so that we can easily reference it. In the example above we have called the container `docker-workshop-mysql` but feel free to call it whatever you like.

We've also included the version tag `:latest`, just in case you have any other versions of the image pulled down to your machine from the previous steps. When you run this command the first time, you will notice you receive an error.

```
error: database is uninitialized and password option is not specified
  You need to specify one of MYSQL_ROOT_PASSWORD, MYSQL_ALLOW_EMPTY_PASSWORD and MYSQL_RANDOM_ROOT_PASSWORD
```

The image we've pulled from the registry is configured to use environment variables for some of its settings. One such setting is what the default root password should be, since MySQL needs to know this in order to initialise the database server. So let's provide that information to the container using the environment variable argument:

```
docker run -e MYSQL_ROOT_PASSWORD=password mysql:latest
```

This will start up the container with one environment variable set: `MYSQL_ROOT_PASSWORD` will be set to `password`.

You will know that this is working if you see the output `Initializing database`. The server will then continue to run through its startup process and eventually notify you that the server is ready for connections.

You'll notice that you have essentially lost control of the command line. This is because the container is running in what is called 'attached' mode. This directly pipes any output from the container's console to your console, but this is a one-way journey, you can't send anything back into the container from here. This gives you a window into what is happening on the server and can be extremely useful when debugging. To exit this mode and stop the container, you have to close the console window entirely.

If you want to run the container in a 'detached' mode, where you don't need to see the output, you can use the `-d` flag, like so:

```
docker run -d -e MYSQL_ROOT_PASSWORD=password mysql:latest
```

Finally, if you want to be able to freely attach/detach from the container, you can run it using the `-i` and `-t` flags. I won't go into the meaning of these flags here, (you can read more in the Docker documentation: https://docs.docker.com/engine/reference/run/) but suffice to say these flags allow input from your console to be passed into the running container so you can successfully detach when you press `Ctrl +P, Ctrl+Q`. To reattach at any time, you can type:

```
docker attach docker-workshop-mysql
```

Finally, a command that may help you in visualising exactly what is going on:

```
docker ps
```

This will list for you all of the currently running containers. You should note that each container has a name and a hash. When running Docker commands targeting a specific container, you can use its name or its hash, entirely interchangeably. Obviously short and simple names are much easier to use however, and that's why we gave our container a simple name. If you don't provide a name, Docker will assign a randomly generated readable name automatically, but these names are still pretty long-winded so providing your own name is highly recommended for the sake of convenience.
