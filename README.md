# Estoque Manager — Vue.js + C#

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

## Endpoints
| Método | Rota | Descrição |
|---|---|---|
| GET | /api/produtos?busca= | Lista/busca produtos |
| GET | /api/produtos/{id} | Detalhe |
| POST | /api/produtos | Cria |
| PUT | /api/produtos/{id} | Atualiza |
| PATCH | /api/produtos/{id}/movimentar?delta=N | Entrada/saída de estoque |
| DELETE | /api/produtos/{id} | Remove |
