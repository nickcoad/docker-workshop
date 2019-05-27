# Exercise 2 - Building a stack

## Docker Compose

In the first exercise we pulled down an image from the Docker registry and started it up in a container. This is a good start, but usually your application will consist of much more than just a database! Your application could be .NET console application, it could be a React application, it could be a PHP application... and so on. So the next question is, how can we create a container with our application on it, and ensure that it can communicate with the database container? The answer to this is Docker Compose.

Docker Compose is a utility that allows Docker images to be run together as a set of containers that are all able to communicate together over a virtual network that Docker Compose will automatically create for you. With one command you can start up an NGINX container hosting your React app, a .NET Core runtime container hosting your backend API and a MySQL container hosting your database - and all three of this containers will be able to communicate among themselves while being isolated from the host machine network.

## Creating a Docker Compose file

The first step in configuring a Docker stack is to create a Docker Compose file. This file is typically named `docker-compose.yml` tells Docker Compose which images it needs to run containers for, and the configuration that should be used for those containers. This includes, among other things:

1. Environment variables
2. Port mappings
3. Volume configuration

You might recall from the first exercise that we have already successfully set environment variables in a running container using the `-e` flag when executing `docker run`. Docker Compose allows you to do the same but in a configuration file based approach rather than having to provide the variable each time you start the container. In fact it's worth noting that all of the above are configurable using the regular docker commands - Docker Compose really shines in the convenience it adds to the process of managing multiple connected containers and doesn't really provide much value if you are only intending on using a single container (for example, a static HTML site that doesn't need a database).

The so lets start simple. Create a new directory on your machine (this directory can exist anywhere) and inside that directory create a new empty file called `docker-compose.yml`. This file will be using a language called YAML (https://yaml.org/spec/1.2/spec.html). YAML is frequently used for some of the same tasks previously performed by JSON or XML, in particular storage of configuration values.

The first thing we need to do in this file is let Docker Compose know what version of the configuration file specification we intend to use. You can see the list of versions here: https://docs.docker.com/compose/compose-file/. We may as well use the latest, so we'll start with:

```yaml
version: "3.7"
```

Even in this early stage, this is a complete and valid `docker-compose.yml` file. You can verify this by running the following command in the same directory:

```
docker-compose up
```

Of course, nothing will happen. You'll receive a message indicating Docker Compose is attaching to _nothing_ and that's it. Docker Compose is attempting to attach to the containers that it has been told to start up, but we haven't got any yet so let's add one.

## Adding a service to the stack

In the previous exercise we retrieved a MySQL image (`mysql:latest`) from the Docker registry and ran it in a container using Docker. Lets do the same thing, but using Docker Compose. The first thing we need to do is tell Docker Compose that we want a service as part of this stack that uses the `mysql` image.

```yaml
version: "3.7"

services:
  database:
    image: mysql:latest
```

This is all we need to tell Docker Compose to run that image in a container as part of our stack. But remember when we tried to run the image using Docker we were told we needed to provide an environment variable specifying the default root password. Using Docker Compose, we can do this:

```yaml
version: "3.7"

services:
  database:
    image: mysql:latest
    environment:
      MYSQL_ROOT_PASSWORD: password
```

To start up your stack, just type:

```
docker-compose up
```

Just like that, we have our MySQL container up and running using Docker Compose! To exit the running stack and shut the services down, press `Ctrl+C`.

## Expanding the stack

Now we'd like a convenient way to manage this database server. One commonly used product for this is called [phpMyAdmin](https://www.phpmyadmin.net/). As it happens, there is a Docker image available for phpMyAdmin on Docker's registry, so let's use that. We tell Docker Compose that we want to add another service to our stack by adding to the services node:

```yaml
version: "3.7"

services:
  database:
    image: mysql:latest
    environment:
      MYSQL_ROOT_PASSWORD: password
  phpmyadmin:
    image: phpmyadmin/phpmyadmin:latest
```

Now when you run `docker-compose up` you'll see two services are started - a `database` service and a `phpmyadmin` service. Looking good so far, but we have a problem: when we try to browse to http://localhost:8080 where we would expect the phpMyAdmin service to be available, we get an error. This is because the Docker Compose network is entirely isolated from your own machine. phpMyAdmin _is_ running on http://localhost:80, but only _inside_ the container, not on your local network. So how do we access this service from our local machine? We map the container port to a local port, and we use that local port to access the service instead:

```yaml
version: "3.7"

services:
  database:
    image: mysql:latest
    environment:
      MYSQL_ROOT_PASSWORD: password
  phpmyadmin:
    image: phpmyadmin/phpmyadmin:latest
    ports:
      - 1001:80
```

When you run this configuration you will now be able to access the service using http://localhost:1001. The port `1001` has been mapped to port `80` inside the `phpmyadmin` container.

**Note:** you can actually use any port number you like. I have chosen `1001` simply because it is an unusual port number and therefore unlikely to conflict with anything else.

There is one final piece of this stack that's missing: phpMyAdmin has no way of knowing where the database server is! If you open up http://localhost:1001 and try to connect using the username 'root' and password 'password', you'll see that it can't connect to the database. We need to configure phpMyAdmin to use the database server we have set up.

Similar to the MySQL image, the creators of the phpMyAdmin image have allowed this setting to be configured using an environment variable: `PMA_HOST`. At this point you might be wondering how you are meant to know these variable names. This information can usually be obtained on the Docker registry page for the particular image you're trying to configure. Most images have pretty great documentation covering the configuration options they support.

So lets configure phpMyAdmin to use the correct database server.

```yaml
version: "3.7"

services:
  database:
    image: mysql:latest
    environment:
      MYSQL_ROOT_PASSWORD: password
  phpmyadmin:
    image: phpmyadmin/phpmyadmin:latest
    ports:
      - 1001:80
    environment:
      PMA_HOST: database
```

Hold on a second! What does 'database' mean? How does phpMyAdmin make sense of that, it doesn't look at all like a server location. The answer is simple - within the Docker Compose virtual network, each container is given a hostname directly corresponding to the service name specified in the `docker-compose.yml` configuration. In our case, we have named the MySQL container 'database'. This means from within the Docker Compose virtual network, that service can be communicated with using 'database' as the hostname. It's possible to also specify a port, but in our case we're running on the default port so phpMyAdmin doesn't need to be told which port to use.

Finally, run the stack one more time using:

```
docker-compose up
```

Now browse to http://localhost:1001 and you should see phpMyAdmin up and running. Enter the root username ('root') and password that we specified ('password'). If all is working well, you should now be logged into phpMyAdmin and connected to our MySQL server.
