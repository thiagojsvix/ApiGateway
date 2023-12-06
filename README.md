# booktiq
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

``Obs.: O endere�o do Servi�oAPI que ser� acessado �: https://jsonplaceholder.typicode.com/users`` 

A propriedade **ServiceName** dentre de `Routes` � utilizado para configurar o [Service Discovery](https://ocelot.readthedocs.io/en/latest/features/servicediscovery.html).
Como ainda n�o est� utilizado o ServiceDisconvery � necess�rio que essa op��o fique em branco.

## Agrega��o
Agrega��o � um mecanismo que existe no OCELOT que ele pode fazer varias requisi��es pelo cliente e agregar o resultados dessas
requisi��es em um novo obejto e retornar esse objeto completo.

Imagine a seguinte situa��o: N�s temos 3 servi�os de API implementados: `Price, Book e Rating`. Para ter a informa��es completa
nosso servi�o web precisaria fazer 3 requisi��es, uma para cada servi�os. Depois precisaria pegar o resultado de cada consulta
e montar uma novo objeto para ter assim o Livro com pre�o e pontua��o.

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

Observe acima que foi adicionado o grupo Aggregates e nele temos a configura��es abaixo:

* **RouteKeys**: Chave dos roteamentos que ser�o chamado pelo rota de agrega��o
* **UpstreamPathTemplate**: path da rota que representa essa agrega��o
* **Aggregator**: Classe C# que ser� responsavel por representar a agrega��o. **O descrito aqui deve ser exatamente o mesmo nome utilizada na classe**;

Se voc� prestou aten��o, nos rotas acima de cada um dos servi�os API foi adicionado os seguintes parametros: `Priority e Key`.
Esse parametros representam a ordem em que as rotas ser�o chamadas e o nome da chave que ser� mencionado na agrega��o respectivamente.

Na Classe `BookDetailsAggregator` descrita abaixo, observe que � necess�rio implentar a interface `IDefinedAggregator` do ocelot
para que ele possa ser invocado quando a rota de agrega��o for acionado.

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

Observe que � implementado o m�todo `Aggregate` reponsavel por toda a l�gica 
de agrega��o assim que as rotas da agrega��o terminarem o seu processo. Em seguida 
� redirecionado para esse m�todo onde devemos remontar um Json e responder a agrega��o desejada.

Para maiors informa��es consultar a documenta��o oficial de [Request Aggregation](https://ocelot.readthedocs.io/en/latest/features/requestaggregation.html#basic-expecting-json-from-downstream-services)

Para testar o servi�o de agrega��o funcionando marque o projeto para exectuar com multiplos projetos e chame o [endere�o](http://localhost:9000/api/v1/books/3872339b-5556-4c94-b7ca-2cc8abde32d8/aggregated-details):


# Tecnologia
[Ocelot](https://github.com/ThreeMammals/Ocelot)
[Docker](https://docs.docker.com/develop/)
[.Net Core 3.1](https://learn.microsoft.com/pt-br/aspnet/core/introduction-to-aspnet-core?view=aspnetcore-8.0)