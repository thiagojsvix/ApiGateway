# booktiq
Projeto de uma bookstore implementando um API Gateway usando o framework OCELOT

## Configuração do Arquivo Ocelot.Json
1. Upstream: para o OCELOT ele é o serviços que está acima de todos, por essa razão ele se entitulo de Upstream. 
Considerando essa definição, todas as configurações que começarem com Upstream estão referenciando ao serviços de ApiGateway
2. DownStream: como o OCELOT se considera o UP os Serviços APIs que estão abaixo dele são considerados DownStream, 
logo todas as configurações que começarem com esse prefixo é sobre o serviço de API.

Abaixo segue um exemplo da configuração

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

### Descrição dos termos no arquivo de configuração

* **DownstreamPathTemplate**: Path para acessar o serviços do API
* **DownstreamScheme**: Define o protocolo que será utilizado. Normalmente `http` ou `http`
* **DownstreamHostAndPorts**: Define o host de destino e a porta do serviços que será utilizado:
* **UpstreamPathTemplate**: Path para orientar o OCELOT qual rota deve utilizar
* **UpstreamHttpMethod**: Verbo ou Método HTTP que será utilizado. Normalmente utilizado o `GET`, mas pode Ser `PUT, POST, DELETE` entre outros

``Obs.: O endereço do ServiçoAPI que será acessado é: https://jsonplaceholder.typicode.com/users`` 

A propriedade **ServiceName** dentre de `Routes` é utilizado para configurar o [Service Discovery](https://ocelot.readthedocs.io/en/latest/features/servicediscovery.html).
Como ainda não está utilizado o ServiceDisconvery é necessário que essa opção fique em branco.

## Agregação
Agregação é um mecanismo que existe no OCELOT que ele pode fazer varias requisições pelo cliente e agregar o resultados dessas
requisições em um novo obejto e retornar esse objeto completo.

Imagine a seguinte situação: Nós temos 3 serviços de API implementados: `Price, Book e Rating`. Para ter a informações completa
nosso serviço web precisaria fazer 3 requisições, uma para cada serviços. Depois precisaria pegar o resultado de cada consulta
e montar uma novo objeto para ter assim o Livro com preço e pontuação.

Para facilitar as coisa o OCELOT implementa essa funcionalidade de [Request Agregation](https://ocelot.readthedocs.io/en/latest/features/requestaggregation.html) para você.

Para configurar essa funcionalidade é necessário adicionar a tag `Aggregates` nas configurações conforme abaixo.

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

Observe acima que foi adicionado o grupo Aggregates e nele temos a configurações abaixo:

* **RouteKeys**: Chave dos roteamentos que serão chamado pelo rota de agregação
* **UpstreamPathTemplate**: path da rota que representa essa agregação
* **Aggregator**: Classe C# que será responsavel por representar a agregação. **O descrito aqui deve ser exatamente o mesmo nome utilizada na classe**;

Se você prestou atenção, nos rotas acima de cada um dos serviços API foi adicionado os seguintes parametros: `Priority e Key`.
Esse parametros representam a ordem em que as rotas serão chamadas e o nome da chave que será mencionado na agregação respectivamente.

Na Classe `BookDetailsAggregator` descrita abaixo, observe que é necessário implentar a interface `IDefinedAggregator` do ocelot
para que ele possa ser invocado quando a rota de agregação for acionado.

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

Observe que é implementado o método `Aggregate` reponsavel por toda a lógica 
de agregação assim que as rotas da agregação terminarem o seu processo. Em seguida 
é redirecionado para esse método onde devemos remontar um Json e responder a agregação desejada.

Para maiors informações consultar a documentação oficial de [Request Aggregation](https://ocelot.readthedocs.io/en/latest/features/requestaggregation.html#basic-expecting-json-from-downstream-services)

Para testar o serviço de agregação funcionando marque o projeto para exectuar com multiplos projetos e chame o [endereço](http://localhost:9000/api/v1/books/3872339b-5556-4c94-b7ca-2cc8abde32d8/aggregated-details):


# Tecnologia
[Ocelot](https://github.com/ThreeMammals/Ocelot)
[Docker](https://docs.docker.com/develop/)
[.Net Core 3.1](https://learn.microsoft.com/pt-br/aspnet/core/introduction-to-aspnet-core?view=aspnetcore-8.0)