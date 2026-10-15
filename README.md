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

## Estrutura

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
```

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
