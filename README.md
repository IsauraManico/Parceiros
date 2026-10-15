Objetivo
Este documento explica como instalar, configurar e executar a API de clientes e fornecedores num computador com Ubuntu, e descreve o que o projeto contém: as tecnologias usadas, a organização dos ficheiros, as classes e a forma de testar cada funcionalidade.
O projeto responde ao enunciado da Fase 2 do teste técnico: uma API em ASP.NET Core, com Entity Framework Core e SQL Server, para gerir clientes e fornecedores de uma única empresa. Uma entidade pode ser cliente, fornecedor ou ambos, e só pode existir um registo por NIF.
São apresentados dois cenários de instalaão da base de dados: com Docker (mais rápido) e sem Docker (SQL Server instalado diretamente no Ubuntu). Basta seguir um deles.
Todos os comandos foram escritos para o terminal do Ubuntu. Os que começam por sudo pedem a palavra-passe do utilizador.


# API de clientes e fornecedores (Teste técnico — Fase 2)

API ASP.NET Core + EF Core + SQL Server para gerir clientes e fornecedores de uma única empresa
(um registo por NIF).

## Versões utilizadas

| Componente | Versão |
|---|---|
| .NET SDK / runtime | 10.0 (LTS) |
| ASP.NET Core / EF Core | 10.0.13 |
| SQL Server | 2022 Developer (contentor Docker) |
| Testes | xUnit 2.9 + `Microsoft.AspNetCore.Mvc.Testing` |

## Estrutura do projeto

```
src/Parceiros.Dominio   Regras de negócio puras: entidade, normalização/validação (NIF, nome, email, limite), exceções
src/Parceiros.Infra     Persistência: DbContext, migrações, repositório (filtros/paginação em SQL), dados de exemplo
src/Parceiros.Api       HTTP: controlador, DTOs, serviço de aplicação, tratamento de erros (ProblemDetails)
tests/Parceiros.Testes  Testes unitários (regras) + integração contra SQL Server real
exemplos.http           Pedidos de exemplo
docker-compose.yml      SQL Server para desenvolvimento
```

## Preparar

1. Subir o SQL Server (porta 1433, utilizador `sa`, palavra-passe de desenvolvimento no `docker-compose.yml`):

   ```bash
   docker compose up -d
   ```

   > Mac com Apple Silicon: no Docker Desktop ative *Settings → General → Use Virtualization framework* e
   > *Use Rosetta for x86/amd64 emulation*; caso contrário a imagem do SQL Server não arranca.

   Sem Docker? Instale o SQL Server Developer/Express e ajuste `ConnectionStrings:Parceiros` em
   `src/Parceiros.Api/appsettings.json`.

2. As migrações são aplicadas automaticamente ao arrancar em `Development`, com alguns dados de exemplo.
   Para aplicá-las manualmente:

   ```bash
   dotnet tool install --global dotnet-ef
   dotnet ef database update -p src/Parceiros.Infra -s src/Parceiros.Api
   ```

## Executar

```bash
dotnet run --project src/Parceiros.Api
```

A API fica em `http://localhost:5176`. Experimente com `exemplos.http` ou `curl`:

```bash
curl -s http://localhost:5176/api/partners | jq
```

## Testar

Com o SQL Server a correr:

```bash
dotnet test
`
Para usar o Docker sem sudo, adicionar o utilizador ao grupo docker e voltar a iniciar a sessão (ou executar newgrp docker):``

sudo usermod -aG docker $USER
newgrp docker
docker --version
docker compose version

Arrancar o SQL Server
Na pasta do projeto existe o ficheiro docker-compose.yml, que define o SQL Server 2022 na porta 1433, com o utilizador sa e a palavra-passe de desenvolvimento indicada nesse ficheiro. Para arrancar:
cd ~/ApiParceiros
docker compose up -d

Cada classe de testes cria uma base de dados própria (`ParceirosTestes_<guid>`) com as migrações e apaga-a no fim.
Para outro servidor: `PARCEIROS_TESTES_CONEXAO="Server=...;User Id=...;Password=...;TrustServerCertificate=True"`.

## Endpoints

| Método | Rota | Resultado |
|---|---|---|
| POST | `/api/partners` | 201 + `Location` + entidade (com `Version`) |
| GET | `/api/partners/{id}` | 200 (mesmo inativa) / 404 |
| GET | `/api/partners?Search&Type&IncludeInactive&Page&PageSize` | 200 `{Items, TotalCount, Page, PageSize}` |
| PUT | `/api/partners/{id}` | 200 com nova `Version` / 400 / 404 / 409 |

Campos: `Id`, `Name`, `TaxNumber`, `Type` (`CUSTOMER`/`SUPPLIER`/`BOTH`), `Email`, `CreditLimit`, `IsActive`,
`Version`, `CreatedAt`, `UpdatedAt` (UTC).

## Decisões de desenho

- **NIF único** — índice único em `Nif` na base de dados (inclui inativos). A API não usa locks em memória:
  dois pedidos simultâneos competem na BD, um ganha (201) e o outro recebe violação de unicidade → 409.
  A BD também tem `CHECK` do formato do NIF e do intervalo do limite de crédito.
- **Concorrência otimista** — `Version` é o `rowversion` do SQL Server em Base64 (8 bytes). O `UPDATE` leva
  `WHERE Id = @id AND Versao = @versaoRecebida`; se não afetar nenhuma linha → 409 sem sobrescrever dados.
- **Erros** — formato único `application/problem+json` (`status`, `title`, `detail`, e `errors` por campo nas
  validações), sem stack traces. 400 pedido/filtro/versão malformados, 404 Id inexistente, 409 NIF duplicado ou versão
  desatualizada, 500 genérico.
- **Validação antes de gravar** — o PUT valida tudo antes de tocar no registo; uma atualização inválida não
  altera nada. O PUT exige todos os campos (`Email` pode ser `null`, mas tem de estar no corpo).
- **Listagem** — filtros, contagem e paginação executados em SQL (`WHERE` → `COUNT` → `ORDER BY Name, Id` → `OFFSET/FETCH`).
  `Search` procura por ocorrência parcial no nome (collation case-insensitive fixada na coluna) ou no NIF
  (com os mesmos separadores removidos); se a pesquisa normalizada ficar vazia, o ramo do NIF é ignorado.
- **Limite de crédito** — `decimal(18,2)`; mais de duas casas decimais é rejeitado (400) em vez de arredondado.
# Parceiros

 Arquitetura
O projeto está dividido em tres camadas, mais os testes. A dependencia vai sempre para dentro: a API conhece a infraestrutura e o domínio; a infraestrutura conhece o domínio; o domínio não conhece ninguem.
Cliente HTTP (Postman, curl, REST Client)
        |
        v
  +-------------------------------------------+
  | Parceiros.Api                             |
  |   Controller -> Servico                   |
  +-----------------+-------------------------+
                    |
        +-----------+-------------+
        v                         v
  +--------------+        +----------------+
  | Parceiros    |------->| Parceiros      |
  | .Infra       |        | .Dominio       |
  | EF Core / SQL|        | Regras         |
  +------+-------+        +----------------+
         |
         v
     SQL Server

Erros
Todos os erros tem o mesmo formato JSON, com os campos title, status e detail. Nos erros de validação existe ainda o campo errors, com as mensagens por campo. Nunca são devolvidos detalhes técnicos.
Código
Quando acontece
400 : Corpo, filtros ou versão malformados; valores inválidos (limite com tres casas decimais, NIF com letras, Page igual a 0, Type desconhecido).
404 :O Id não existe.
409 NIF ja registado ou versão desatualizada.
500 : Erro inesperado. A mensagem é genérica; o detalhe fica apenas no registo do servidor.

Como testar
1. Testes automáticos
Com o SQL Server a correr (cenário A ou B), a partir da pasta do projeto:
dotnet test
No fim deve aparecer "Passed: 67". Cada classe de testes cria uma base de dados temporária com nome ParceirosTestes_ seguido de um identificador, aplica as migraçõess e apaga-a no fim, por isso os testes não alteram os dados da base de dados Parceiros.
Por defeito, os testes ligam-se a localhost,1433 com o utilizador sa e a palavra-passe do docker-compose.yml. No cenário B, ou noutro servidor, indicar a ligação (sem o nome da base de dados):
export PARCEIROS_TESTES_CONEXAO="Server=localhost,1433;User Id=sa;Password=<palavra-passe>;TrustServerCertificate=True;Encrypt=False"
dotnet test
Para executar apenas um grupo de testes:
dotnet test --filter "FullyQualifiedName~RegrasTestes"    # so os unitarios, sem base de dados
dotnet test --filter "FullyQualifiedName~C5"              # so os de concorrencia

#Testes manuais com curl

Com a API a correr, os comandos seguintes percorrem os cenários do enunciado. Nos comandos que indicam <ID> e <VERSION>, usar os valores devolvidos pela resposta de criação.

Cenário 1 - Normalização e NIF duplicado

curl -i -X POST http://localhost:5176/api/partners -H 'Content-Type: application/json' \
  -d '{"Name":"  Ana Silva  ","TaxNumber":"001.234.568","Type":"CUSTOMER","CreditLimit":100}'
Resultado esperado: 201. O nome fica "Ana Silva" e o NIF "001234568". Repetir com "001 234 568" no NIF: resultado 409. O NIF continua reservado depois de a entidade ser desativada.

Cenário 2 - Filtros por tipo
Criar tres entidades com nomes que contenham a mesma palavra (por exemplo "Teste") e tipos CUSTOMER, SUPPLIER e BOTH, com NIF diferentes. Depois:

curl "http://localhost:5176/api/partners?Search=Teste&Type=CUSTOMER"   # cliente e ambos
curl "http://localhost:5176/api/partners?Search=Teste&Type=SUPPLIER"   # fornecedor e ambos
curl "http://localhost:5176/api/partners?Search=Teste&Type=BOTH"       # só ambos

Cenário 3 - Desativar e reativar

curl -i -X PUT http://localhost:5176/api/partners/<ID> -H 'Content-Type: application/json' \
  -d '{"Name":"Ana Silva","TaxNumber":"001234568","Type":"CUSTOMER","Email":null,"CreditLimit":100,"IsActive":false,"Version":"<VERSION>"}'
Resultado esperado: 200. A entidade deixa de aparecer em GET /api/partners, aparece com IncludeInactive=true e continua acessível em GET /api/partners/<ID>. Para reativar, repetir o PUT com IsActive verdadeiro e a Version devolvida pelo PUT anterior.

Cenário 4 - Atualizar e NIF de outra entidade
Atualizar uma entidade mantendo o próprio NIF devolve 200. Alterar o NIF para o de outra entidade devolve 409 e os dados anteriores mantém-se (pode confirmar-se com um GET pelo Id).

Cenário 5 - Concorrencia
Repetir o mesmo PUT, com a mesma Version, duas vezes: a primeira devolve 200 e a segunda 409. Para o caso de dois pedidos verdadeiramente simultaneos, usar os testes automticos C5, que disparam dez criações em paralelo com o mesmo NIF e verificam que existe um 201, nove 409 e um único registo:

dotnet test --filter "FullyQualifiedName~C5"

Cenário 6 - Erros 400

# CreditLimit com tr�s casas decimais
curl -i -X POST http://localhost:5176/api/partners -H 'Content-Type: application/json' \
  -d '{"Name":"Teste","TaxNumber":"123456780","Type":"BOTH","CreditLimit":10.999}'
 
# NIF com letras
curl -i -X POST http://localhost:5176/api/partners -H 'Content-Type: application/json' \
  -d '{"Name":"Teste","TaxNumber":"12345678A","Type":"BOTH","CreditLimit":1}'
 
# Page igual a 0 e Type inv�lido
curl -i "http://localhost:5176/api/partners?Page=0"
curl -i "http://localhost:5176/api/partners?Type=OUTRO"
 
# Version malformada
curl -i -X PUT http://localhost:5176/api/partners/<ID> -H 'Content-Type: application/json' \
  -d '{"Name":"Ana Silva","TaxNumber":"001234568","Type":"CUSTOMER","Email":null,"CreditLimit":100,"IsActive":true,"Version":"isto-nao-e-versao"}'

Todos devolvem 400 e nenhum altera dados.
Cenário 7 - Filtros combinados, total e desempate

curl "http://localhost:5176/api/partners?Search=Teste&Type=CUSTOMER&Page=1&PageSize=2"
curl "http://localhost:5176/api/partners?Search=Teste&Type=CUSTOMER&Page=2&PageSize=2"
curl "http://localhost:5176/api/partners?Search=Teste&Page=99"
curl "http://localhost:5176/api/partners?Search=Ana"

O TotalCount é o mesmo em todas as páginas do mesmo filtro. A pégina 99 devolve Items vazio e o TotalCount correto. A pesquisa "Ana" devolve apenas entidades cujo nome contém "Ana", e nunca todas as entidades através do campo do NIF. Quando há nomes iguais, a ordem entre eles a dada pelo Id, de forma estável entre páginas.
12.3. Testar com o Postman
    1. No Postman, escolher Import e selecionar o ficheiro Parceiros.postman_collection.json.
    2. Abrir a coleção "API Parceiros". A variável host aponta para http://localhost:5176.
    3. Executar os pedidos por ordem. O primeiro ("Criar cliente") guarda o Id e a Version nas variáveis da coleção, que os pedidos seguintes utilizam.
    4. Para criar pedidos a parte: método POST, URL http://localhost:5176/api/partners, separador Body, opçãoo raw e formato JSON.

O que significa cada grupo de testes automáticos
Testes
O que verificam
RegrasTestes (29): Normalização do NIF com varios separadores; rejeição de NIF com 8 ou 10 dígitos, letras, digitos não ASCII e vazio; Trim do nome e limites de 2 e 100 caracteres; email vazio convertido em nulo e formatos de email; limite de crédito nos extremos (0 e 1 000 000) e valores inválidos; tipo inválido.
C1_ (3) : Criação com normalizacao, cabeçalho Location e datas UTC; NIF repetido noutro formato devolve 409, inclusive depois de desativar; email só com espaços fica nulo.
C2_ (1) : Filtros por tipo: o BOTH aparece nos tres; o CUSTOMER não aparece no filtro SUPPLIER.
C3_ (1):Desativar, listar com e sem IncludeInactive, ler pelo Id e reativar.
C4_ (2) :Atualização mantendo o próprio NIF; mudança para o NIF de outra entidade devolve 409 e mantém dados e versão.
C5_ (3):Dez criações simultaneas com o mesmo NIF; duas atualizações simultaneas com a mesma versão; versão antiga depois de outra atualização.
C6_ (22):Valores inválidos na criação; atualização inválida sem alterações; filtros inválidos; versão malformada; campos em falta no PUT; JSON malformado.
GET_ e PUT_ (2):Id inexistente devolve 404, tanto na leitura como na atualização.
C7_ (4):Filtros combinados; TotalCount antes da paginação; páginas sem repetiçõeses; desempate por Id; página além dos resultados; pesquisa por nome sem corresponder a todos pelo NIF; pesquisa por NIF com separadores.

