<img width="100%" alt="Halloween 2026" src="https://baltaio.blob.core.windows.net/static/images/v4/challenges/halloween-2026/banner.jpg" />

## 🎃 Halloween - Desafio 2

Oi, eu sou o [seu nome aqui] e este é o espaço onde compartilho minha jornada de aprendizado durante o desafio **Halloween 2026**, realizado pelo [balta.io](https://balta.io). 👻

Aqui você vai encontrar projetos, exercícios e códigos que estou desenvolvendo durante o desafio.

### Sobre este desafio

Neste desafio o objetivo é criar um encurtador de URLs completo utilizando os conceitos de Spec-Driven Development

#### O que foi implementado

- API em .NET 10 para criar links com código automático ou próprio, validar URLs e definir expiração opcional em UTC.
- Redirecionamento e consulta pública de estatísticas de acesso por código.
- Persistência em SQLite com índice único para códigos e incremento atômico do contador de acessos.
- Testes unitários e de integração para os fluxos, incluindo concorrência e persistência após reinício.

Neste processo eu aprendi:

- ✅ Transformar especificação, constituição e plano em decisões de arquitetura e testes verificáveis.
- ✅ Tratar colisões de códigos com unicidade no banco e nova tentativa para códigos automáticos.
- ✅ Testar concorrência e persistência após reinício usando um banco temporário.

## Sobre a aplicação

O projeto usa .NET 10, ASP.NET Core Minimal APIs, Entity Framework Core e
SQLite, com validação de URL de destino e expiração em UTC.

## Requisitos

- .NET 10 SDK.
- PowerShell.

## Executar localmente

Abra o PowerShell e vá para a raiz do repositório:

```powershell
Set-Location 'C:\Private\Courses\Balta\desafio 2\balta-desafio-halloween-2026-spec-driven-02'
```

Execute os comandos abaixo **a partir da raiz**:

```powershell
# Restaurar as dependências da solução
dotnet restore src\LinkShortener.sln

# Habilitar a documentação interativa (Scalar roda só em Development)
$env:ASPNETCORE_ENVIRONMENT = 'Development'

# Iniciar a API em http://localhost:5102
dotnet run --project src\LinkShortener.Api --urls http://localhost:5102
```

As migrações são aplicadas automaticamente ao iniciar. Com a API em execução,
acesse a documentação em [http://localhost:5102/scalar](http://localhost:5102/scalar).

Para encerrar a aplicação, pressione **Ctrl+C** no terminal em que `dotnet run`
está em execução.

### Endpoints

| Método | Rota                      | Descrição                                                                 |
| ------ | ------------------------- | ------------------------------------------------------------------------- |
| `POST` | `/api/links`              | Cria um link com código automático ou próprio e expiração opcional (UTC). |
| `GET`  | `/{code}`                 | Redireciona com `302` para a URL de destino e incrementa o contador.      |
| `GET`  | `/api/links/{code}/stats` | Retorna o código e o número de acessos.                                   |

Exemplo de criação:

```powershell
Invoke-RestMethod -Method Post -Uri http://localhost:5102/api/links `
  -ContentType 'application/json' -Body '{"targetUrl":"https://example.com"}'
```

### Comandos úteis

Execute-os a partir da raiz do repositório:

```powershell
# Compilar a solução
dotnet build src\LinkShortener.sln

# Executar os testes unitários e de integração
dotnet test src\LinkShortener.sln
```

## Estrutura da solução

- `src\LinkShortener.Api`: endpoints HTTP, serviços e persistência SQLite.
- `src\LinkShortener.Tests`: testes unitários e de integração.
- `specs`: constituição, especificação, plano e tarefas.

O banco SQLite fica em `src\LinkShortener.Api\linkshortener.db`, conforme a
connection string `DefaultConnection` em `appsettings.json`.

## Bagde

<img src="https://baltaio.blob.core.windows.net/static/images/v4/challenges/halloween-2026/02.png" width="200" />

## Problema

--

## Sobre o Halloween 2026

O desafio **Halloween 2026** consiste em implementar implementar o modelo Spec Driven Development de ponta a ponta, criando apps completas com IA.

### Veja meu progresso no desafio

[Meu progresso no GitHub](https://github.com/williamlimasilva/balta-desafio-halloween-2026-spec-driven-02)
