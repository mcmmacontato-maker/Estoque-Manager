# Estoque Manager

Gerenciador de produtos/estoque com **Vue 3 (Vite)** no frontend e **ASP.NET Core 8 Web API + EF Core (SQLite)** no backend.

## Estrutura
```
backend/EstoqueApi   → API REST (http://localhost:5000, Swagger em /swagger)
frontend             → SPA Vue 3 (http://localhost:5173, proxy /api → 5000)
```

## Pré-requisitos
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Node.js 18+

## Como rodar
```powershell
# Terminal 1 - backend
cd backend/EstoqueApi
dotnet run

# Terminal 2 - frontend
cd frontend
npm install
npm run dev
```

## Testes
```powershell
cd backend
dotnet test        # 25 testes de integração (CRUD, validação, concorrência, segurança)
```

## Segurança
- Validação de entrada com DTO (sem *mass assignment*) e respostas `ProblemDetails`
- Rate limiting (120 req/min por IP), limite de corpo (32 KB) e de busca
- Cabeçalhos de segurança (CSP, `X-Frame-Options`, `nosniff`, etc.)
- Movimentação de estoque atômica (sem condição de corrida)
- Swagger apenas em desenvolvimento; erros sem stack trace
- Build falha se houver dependência vulnerável; CI roda testes e auditorias a cada push

> ⚠️ A API ainda **não possui autenticação** — não publique em rede pública sem adicioná-la.

## Endpoints
| Método | Rota | Descrição |
|---|---|---|
| GET | /api/produtos?busca= | Lista/busca produtos |
| GET | /api/produtos/{id} | Detalhe |
| POST | /api/produtos | Cria |
| PUT | /api/produtos/{id} | Atualiza |
| PATCH | /api/produtos/{id}/movimentar?delta=N | Entrada/saída de estoque |
| DELETE | /api/produtos/{id} | Remove |
