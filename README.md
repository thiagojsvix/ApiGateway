# API Gateway
Projeto de uma bookstore implementando um API Gateway usando o framework OCELOT

## Configura��o do Arquivo Ocelot.Json
1. Upstream: para o OCELOT ele � o servi�os que est� acima de todos, por essa raz�o ele se entitulo de Upstream. 
Considerando essa defini��o, todas as configura��es que come�arem com Upstream est�o referenciando ao servi�os de ApiGateway
2. DownStream: como o OCELOT se considera o UP os Servi�os APIs que est�o abaixo dele s�o considerados DownStream, 
logo todas as configura��es que come�arem com esse prefixo � sobre o servi�o de API.

Abaixo segue um exemplo da configura��o

```json
{
  "Routes": [
    {
      "ServiceName": "",
      "DownstreamPathTemplate": "/users",
      "DownstreamScheme": "https",
      "DownstreamHostAndPorts": [
        {
          "Host": "jsonplaceholder.typicode.com",
          "Port": 443
        }
      ],
      "UpstreamPathTemplate": "/metadata/users",
      "UpstreamHttpMethod": [ "Get" ]
    }
}
```

### Descri��o dos termos no arquivo de configura��o

* **DownstreamPathTemplate**: Path para acessar o servi�os do API
* **DownstreamScheme**: Define o protocolo que ser� utilizado. Normalmente `http` ou `http`
* **DownstreamHostAndPorts**: Define o host de destino e a porta do servi�os que ser� utilizado:
* **UpstreamPathTemplate**: Path para orientar o OCELOT qual rota deve utilizar
* **UpstreamHttpMethod**: Verbo ou M�todo HTTP que ser� utilizado. Normalmente utilizado o `GET`, mas pode Ser `PUT, POST, DELETE` entre outros

``Obs.: O endere�o do Servi�o API que ser� acessado �: https://jsonplaceholder.typicode.com/users`` 

A propriedade **ServiceName** dentre de `Routes` � utilizado para configurar o [Service Discovery](https://ocelot.readthedocs.io/en/latest/features/servicediscovery.html).
Como ainda n�o est� utilizado o ServiceDisconvery � necess�rio que essa op��o fique em branco.

## Agrega��o
Agrega��o � um mecanismo que existe no OCELOT que ele pode fazer varias requisi��es pelo cliente e agregar o resultados dessas requisi��es em um novo obejto e retornar esse objeto completo.

Imagine a seguinte situa��o: N�s temos 3 servi�os de API implementados: `Price, Book e Rating`. Para ter a informa��o completa nosso servi�o web precisaria fazer 3 requisi��es, uma para cada servi�os. Depois precisaria pegar o resultado de cada consulta e montar uma novo objeto para ter assim o Livro com pre�o e pontua��o.

Para facilitar as coisa o OCELOT implementa essa funcionalidade de [Request Agregation](https://ocelot.readthedocs.io/en/latest/features/requestaggregation.html) para voc�.

Para configurar essa funcionalidade � necess�rio adicionar a tag `Aggregates` nas configura��es conforme abaixo.

```json
{
  "Routes": [
    {
      "ServiceName": "",
      "DownstreamPathTemplate": "/api/books/{id}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "book-service",
          "Port": 9001
        }
      ],
      "UpstreamPathTemplate": "/api/v1/books/{id}/details",
      "UpstreamHttpMethod": [ "Get" ],
      "Priority": 2,
      "Key": "Book"
    },

    {
      "ServiceName": "",
      "DownstreamPathTemplate": "/api/booksprices/{id}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "pricing-service",
          "Port": 9002
        }
      ],
      "UpstreamPathTemplate": "/api/v1/books/{id}/price",
      "UpstreamHttpMethod": [ "Get" ],
      "Priority": 1,
      "Key": "BookPrice"
    },

    {
      "ServiceName": "",
      "DownstreamPathTemplate": "/api/booksratings/{id}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "rating-service",
          "Port": 9003
        }
      ],
      "UpstreamPathTemplate": "/api/v1/books/{id}/ratings",
      "UpstreamHttpMethod": [ "Get" ],
      "Priority": 0,
      "Key": "BookRating"
    }
  ],
  "Aggregates": [
    {
      "RouteKeys": [
        "Book",
        "BookPrice",
        "BookRating"
      ],
      "UpstreamPathTemplate": "/api/v1/books/{id}/aggregated-details",
      "Aggregator": "BookDetailsAggregator"
    }
  ]
}
```

Observe acima que foi adicionado o grupo Aggregates e nele temos a configura��s abaixo:

* **RouteKeys**: Chave dos roteamentos que ser� chamado pelo rota de agrega��o
* **UpstreamPathTemplate**: path da rota que representa essa agrega��o
* **Aggregator**: Classe C# que ser� responsavel por representar a agrega��o. **O descrito aqui deve ser exatamente o mesmo nome utilizada na classe**;

Se voc� prestou aten��o, nos rotas acima de cada um dos servi�os API foi adicionado os seguintes parametros: `Priority e Key`.

Esse parametros representam a ordem em que as rotas ser�o chamadas e o nome da chave que ser� mencionado na agrega��o respectivamente.

Na Classe `BookDetailsAggregator` descrita abaixo, observe que � necess�rio implentar a interface `IDefinedAggregator` do ocelot para que ele possa ser invocado quando a rota de agrega��o for acionado.

```csharp
 public class BookDetailsAggregator : IDefinedAggregator
    {
        public async Task<DownstreamResponse> Aggregate(List<HttpContext> responses)
        {
            var resultingAggregation = new ResultingAggregation<Book>();

            HttpResponseMessage response = new HttpResponseMessage();

            try
            {
                var book = await responses[0].Items.DownstreamResponse().Content.ReadAsAsync<Book>();

                try
                {
                    var price = await responses[1].Items.DownstreamResponse().Content.ReadAsAsync<BookPrice>();
                    var rating = await responses[2].Items.DownstreamResponse().Content.ReadAsAsync<BookRating>();

                    book.Stars = rating.Stars;
                    book.Price = price.Price;

                    resultingAggregation.Ok(book);
                }
                catch (Exception)
                {
                    resultingAggregation.Partial(book, "There was an error when loading Book DownStreams.");
                }
            }
            catch (Exception)
            {
                resultingAggregation.Error("There was error when loading the book details.");
            }

            response.Content = new ObjectContent<ResultingAggregation<Book>>(resultingAggregation, new JsonMediaTypeFormatter());

            return new DownstreamResponse(response);
        }
    }
```

Observe que � implementado o m�todo `Aggregate` reponsavel por toda a l�gica de agrega��o assim que as rotas da agrega��o terminarem o seu processo. Em seguida � redirecionado para esse m�todo onde devemos remontar um Json e responder a agrega��o desejada.

Para maiors informa��es consultar a documenta��o oficial de [Request Aggregation](https://ocelot.readthedocs.io/en/latest/features/requestaggregation.html#basic-expecting-json-from-downstream-services)


## Configurando Projeto para rodar no Docker
Para facilitar nossos testes vamos iniciar a configura��o do projeto no docker. Para tal faz se necess�rio criar os arquivos de configura��o do docker.

![docker settins](./imgs/docker-settings.png)

Observe na imagem acima que foi adicionado o arquivo **DockerFile** em todos os projeto, inclusive no projetos `pricing-service` e `rating-service`. Tamb�m foi adicionado o arquivo `docker-compose-yml` e `.dockerignore`.   

> Aten��o para o local que foi adicionado o arquivo `docker-compose.yml`

### Arquivo Dockerfile 

```docker
# Stage 1
FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build
WORKDIR /build
EXPOSE 9001
COPY . .
RUN dotnet restore
RUN dotnet publish -c Release -o /app

# Stage 2
FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS final
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "book-service.dll"]
```

> Observer que est� sendo exposto a porta 9001 que � a mesma porta configurada no API.
> Essa mesma configura��o se repete para os demais servi�os de API

### Arquivo docker-compose.yml

```yml
version: '3.7'
services:
  book-service:
    image: book-service:booktiq-apiservice
    build:
        context: ./src/book-service
        dockerfile: Dockerfile
    container_name: book-service-api
    hostname: book-service-host

  pricing-service:
    image: pricing-service:booktiq-apiservice
    build: ./src/pricing-service
    container_name: pricing-service

  rating-service:
    image: book-rating:booktiq-apiservice
    build: ./src/rating-service
    container_name: rating-service

  api-gateway:
    image: api-gateway:booktiq-apiservice
    build: ./src/api-gateway
    container_name: api-gateway
    ports:
        - 9000:9000

  consul:
    image: consul:1.15.4
    container_name: "consul"
    hostname: "consul"
    ports:
        - 8500:8500
    command: agent -server -ui -node=server-1 -bootstrap-expect=1 -client=0.0.0.0
```
> Para entender a diferen�a entre build do servi�o book-service e os demais acesso o [link](https://docs.docker.com/compose/compose-file/build/#attributes)

No arquivo `docker-compose` estamos defindo cada um dos servi�os do projeto.
Observe que os �nicos que est�o com o parametros ports definido � o `api-gateway` e `consul`, sendo assim s�o os �nicos que poderam ser acessados externamento.

> O nome que � dado ao servi�o no arquivo `docker-compose` deve ser o mesmo nome de servi�o no arquivo de configura��o do OCELOT.

```json
  "Routes": [
    {
      "ServiceName": "book-service-api",
      "LoadBalancerOptions": {
        "Type": "LeastConnection"
      },
      "DownstreamPathTemplate": "/api/books/{id}",
      "DownstreamScheme": "http",
      "UpstreamPathTemplate": "/api/v1/books/{id}/details",
      "UpstreamHttpMethod": [ "Get" ],
      "Priority": 2,
      "Key": "Book"
    },
```

Observe no trecho de configura��o acima que a propriedade ServiceName da rota tem o exato nome do servi�os no arquivo `docker-compose`.

O nome do servi�o `book-service-api` saiu do padr�o justamente para explicar a rela��o entre a propriedade `ServiceName do OCELOT` e nome do servico no `docker-compose`

Caso queira executar o servi�o do consul separadamente, utilize o comando abaixo:

`docker run -d -p 8500:8500 -p 8600:8600/udp --name=badger consul agent -server -ui -node=server-1 -bootstrap-expect=1 -client=0.0.0.0`

### Rodar projeto com docker-compose
Para executar o projeto com docker compose execute os comandos abaixo:
```powershell
clear; docker compose down --remove-orphans --rmi local; docker compose up -d
```

Esse comando primeiro vai limpar a tela do prompt depois, vai tentar remover todos os servi�os que est�o listados nos docker-compose em seguida tenta criar as imagens e subir os containers.

## Configura o Service Discovery com CONSUL

Com j� foi observado no arquivo `docker-compose` estamos iniciando um container do Consul. O `Consul` ser� utilizado juntamente do `OCELOT` para facilitar a descoberta de servi�os na rede. 

Como os nossa api est�o configuradas no docker e n�o temos controle de sua configura��o de rede, fica mais dificil para nossas aplica��o web acessarem esse servi�os de api. 

Para resolver esse problema e n�o termos que mudar a configura��o de nossos clientes toda vez que fizermos atualiza��o de container de servi�o API, iremos utilizar o `CONSULT` para identificar esse novo container e deixa-lo acessivel a nossa `API Gateway`.

```json
{
      "ServiceName": "",
      "DownstreamPathTemplate": "/api/books/{id}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "book-service",
          "Port": 9001
        }
      ],
      "UpstreamPathTemplate": "/api/v1/books/{id}/details",
      "UpstreamHttpMethod": [ "Get" ],
      "Priority": 2,
      "Key": "Book"
    },
```

Conforme pode ser visto no trecho de configura��o do ocelot acima. Anteriomente quando tinhamos que configurar um novo servi�os, tinhamos que definir o host e porta no servi�o API. Como pode ser percebido no arquivo abaixo, com auxio do Servi�o de Discoberta do Consul, n�o ser� mais necess�rio.

```json
"GlobalConfiguration": {
    "ServiceDiscoveryProvider": {
      "Scheme": "http",
      "Host": "consul",
      "Port": 8500,
      "Type": "Consul"
    }
  },
  "Routes": [
    {
      "ServiceName": "book-service-api",
      "LoadBalancerOptions": {
        "Type": "LeastConnection"
      },
      "DownstreamPathTemplate": "/api/books/{id}",
      "DownstreamScheme": "http",
      "UpstreamPathTemplate": "/api/v1/books/{id}/details",
      "UpstreamHttpMethod": [ "Get" ],
      "Priority": 2,
      "Key": "Book"
    }
  ]
}
```

Na se��oo de configura��o das rotas das API agora s� � necess�rio informar o `ServiceName` e o `LoadBalancerOptins` e tamb�m temos que fazer referencia ao ao Servi�o de Discoberta na se��o `GlobalConfiguration:ServiceDiscoveryProvider` 

Vale destacar que o parametro `host` e `type` possuem o mesmo nome por�m com funcionalidade diferente:
* Host: serve para identificar o nome do servi�o na rede que ira fazer o ServiceDiscovery. Essa configura��o deve ser a mesma especificada no parametro `hostname` do `docker-compose`;
* Type: serve para identificar qual dos tipo de servi�o de descoberta suporta pelo OCELOT est� sendo utilizando;

J� a propriedade `ServiceName` da configura��o de `Routes` deve representar o mesmo nome de servi�o configura��o no arquivo` AppSettings.json`. Essa configura��o � enviado para Consul para ser registrado o servi�o com esse nome.

> Para maiores informa��es sobre como configurar o servi�os de descoberta do OCELOT, consulte esse [link](https://ocelot.readthedocs.io/en/latest/features/servicediscovery.html#service-discovery).

Para finalizar a configura��o do Servi�o de API-Gateway faz-se necess�rio chamar o servi�o na classe `Startup.cs` do projeto.

![AddConsul](./imgs/AddConsul.png)
![AddConsul](./imgs/dependencia-consul.png)

Daqui pra frente temos que configurar cada um dos servi�os de API para se registrar no `CONSUL` assim que forem iniciados.

### Configura��es nos Servi�os de API para registrar no Consult
A primeira coisa que temos que fazer � adicionar o pacote do Consul ao projeto. Para fazer isso voc� pode usar o comando abaixo
`dotnet add package Consul --version 1.6.1.1`

![AddConsul](./imgs/dependencia-consul-api.png)

Deve ficar conforme apresentado na imagem acima.

Em seguida temos que realiar algumas configura��es na aplica��o conforme apresentado na imagem abaixo.

![configuracao-consul-api](./imgs/configuracao-consul-api.png)

Na imagem acima estamos destacando quais os arquivos que sofreram altera��es para efetuar a configura��o de auto registro do servi�os de API.

Abaixo iremos destar arquivo por arquivo e comentar os principais pontos de cada um.

![Startup-api](./imgs/startup-api.png)

No arquivo de Startup do projeto de API temos o destaque para o m�todo ConfigureService onde adicionamos a extens�o AddCOnsultSettings. Depois fazemos a chamado do servi�o do Consul no m�todo Configure passando a propriedade `ServiceSettins` que j� foi instanciada no m�todo ConfigureServices;

![service-registry-extensions](./imgs/service-registry-extensions.png)

No m�todo AddConsultSettings estamos adicionado ao container de inje��o o endere�o do servi�o de discoberta.
No m�todo UseConsult fazendo o registro do API no consul, e logando o que est� ocorrendo;

![startup-boostrap-extensions](./imgs/startup-boostrap-extensions.png)

Aqui na classe StartupBoostrapExtensions � feito a leitura das configura��es feita no AppSettings.json e retorna para o m�todo que o invocou.


```json
{
  "ServiceSettings": {
    "ServiceName": "book-service-api",
    "ServiceHost": "book-service-host",
    "ServicePort": 9001,
    "ServiceDiscoveryAddress": "http://consul:8500"
  },
}
```

Acima � descrito a configura��o `ServiceSettings`` do arquivo appsettings.json iremos descrever o que � cada uma das propriedades:
* **ServiceName**: Nome do servi�o que ser� registrado no Consul. Esse nome deve ser o mesmo configura��o no arquivo `Ocelot.json`
* **ServiceHost**: Nome que ser� utilizado para identificar a m�quina que est� rodando o Servi�o. Se for execu��o local, pode ser `Localhost` ou `IP da m�quina`, quando estiver usando `docker` devemos colocar o _`nome do host do container ou um de seus alias de rede`_;

![docker-compose-hostname](./imgs/docker-compose-hostname.png)

Como pode ser visto na imagem acima o hostname do servi�o est� definido como `book-service-host`, por�m esse n�o � o �nico nome na qual o contaner responder. Utilizando o comando abaixo � poss�vel identificar as configura��es interna do container e verificar os `alias de rede` que o container responde. Seria poss�vel informar qualquer um desses nomes na propriedade `ServiceHost`

```powershell
docker container inspect book-service-api
```

Abaixo sa�da do comando listado acima;
![docker-compose-alias](./imgs/docker-compose-alias.png)


* **ServicePort**: Porta que o servi�o foi configurado. Essa configura��o pode ser obtida na arquivo Program.cs onde � definido a propriedade `ListenAnyIP`. Quando estiver utilizando docker e o servi�os estiver externo deve ser adiciona a porta que fica antes do :, conforme pode ser visto no exemplo abaixo:

![config-port-docker](./imgs/config-port-docker.png)

A porta defina com n�mero 1 � a porta que ser� acessado pelo browser ou porta externa, j� a porta n�mero 2 � a porta que est� configura��o no arquivo `Program.cs` ou porta interna.

* **ServiceDiscoveryAddress**:


```csharp
namespace BookService.Settings
{
    public class ServiceSettings
    {
        public string ServiceName { get; set; }
        public string ServiceHost { get; set; }
        public int ServicePort { get; set; }
        public string ServiceDiscoveryAddress { get; set; }
    }
}
```
A classe `ServiceSettings` � um representa��o em objeto das configura��es feita no arquivo `appsettings.json`. 
Para acessar o Consul e verificar os servi�os de API registrados, basta acessar a [url](http://localhost:8500/ui).

> Lembra que essa configura��o deve ser replicada para cada um dos servi�o de API da solu��o.

![consul-ui](./imgs/consul-ui.png)

# Tecnologia
[Ocelot](https://github.com/ThreeMammals/Ocelot)
[Docker](https://docs.docker.com/develop/)
[Consul](https://developer.hashicorp.com/consul/tutorials/developer-discovery)
[.Net Core 3.1](https://learn.microsoft.com/pt-br/aspnet/core/introduction-to-aspnet-core?view=aspnetcore-8.0)