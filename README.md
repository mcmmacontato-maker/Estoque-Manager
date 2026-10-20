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
dotnet test        # 60 testes de integração (CRUD, auth, perfis, histórico, painel, demo, segurança)
```

## Segurança
- Validação de entrada com DTO (sem *mass assignment*) e respostas `ProblemDetails`
- Rate limiting (120 req/min por IP), limite de corpo (32 KB) e de busca
- Cabeçalhos de segurança (CSP, `X-Frame-Options`, `nosniff`, etc.)
- Movimentação de estoque atômica (sem condição de corrida)
- Swagger apenas em desenvolvimento; erros sem stack trace
- Build falha se houver dependência vulnerável; CI roda testes e auditorias a cada push

## Autenticação
Login por e-mail e senha com sessão em **cookie HttpOnly + SameSite=Strict** (o JavaScript não acessa o token).

| Perfil | Pode |
|---|---|
| **Admin** | Tudo: cadastrar, editar, excluir e movimentar |
| **Operador** | Consultar e dar entrada/saída de estoque |

**Usuários de desenvolvimento** (definidos em `appsettings.Development.json`):

| E-mail | Senha | Perfil |
|---|---|---|
| admin@estoque.local | Admin@2026 | Admin |
| operador@estoque.local | Operador@2026 | Operador |

### Modo demonstração
Com `Demo:Ativo = true` (ligado em desenvolvimento), a tela de login mostra os botões
**Entrar como Administrador** e **Entrar como Operador** — o visitante entra com um clique, sem senha.
Os dados voltam ao catálogo original a cada `Demo:ResetMinutos` (padrão 60). Fora do modo demo,
o endpoint de login rápido nem é registrado.

```powershell
$env:Demo__Ativo="true"; $env:Demo__ResetMinutos="60"   # para publicar como portfólio
```

Proteções: senha com hash PBKDF2, bloqueio de 15 min após 5 tentativas, limite de 10 logins/min por IP,
mensagem genérica (não revela se o e-mail existe), sessão de 8h invalidada se o usuário for desativado.

**Produção:** não use as senhas acima. Defina os usuários iniciais por variável de ambiente, por exemplo:
```powershell
$env:Seed__Usuarios__0__Email="admin@suaempresa.com"
$env:Seed__Usuarios__0__Nome="Administrador"
$env:Seed__Usuarios__0__Senha="<senha forte>"
$env:Seed__Usuarios__0__Perfil="Admin"
```
e publique atrás de **HTTPS** (o cookie é marcado `Secure` em produção).

## Telas
| Tela | Conteúdo |
|---|---|
| **Painel** | Valor do estoque, entradas/saídas em 14 dias, gráfico diário, itens para repor, valor por categoria, atividade recente |
| **Produtos** | Catálogo com busca, filtros por status, ajuste rápido +/−, cadastro/edição/exclusão (Admin) |
| **Movimentações** | Registro de entradas e saídas com saldo previsto e histórico filtrável por produto, tipo e período |

Toda alteração de quantidade (entrada, saída, ajuste na edição ou cadastro) gera um registro imutável
com usuário, data e saldo resultante. O histórico é mantido mesmo se o produto for excluído.

## Endpoints
| Método | Rota | Descrição | Perfil |
|---|---|---|---|
| POST | /api/auth/login | Login com e-mail e senha | — |
| POST | /api/auth/demo | Login rápido (só no modo demo) | — |
| POST | /api/auth/logout | Encerra a sessão | logado |
| GET | /api/auth/me | Usuário atual | logado |
| GET | /api/produtos?busca= | Lista/busca produtos | logado |
| GET | /api/produtos/{id} | Detalhe | logado |
| POST | /api/produtos | Cria | Admin |
| PUT | /api/produtos/{id} | Atualiza | Admin |
| PATCH | /api/produtos/{id}/movimentar?delta=N | Entrada/saída de estoque | logado |
| DELETE | /api/produtos/{id} | Remove | Admin |
| GET | /api/movimentacoes?produtoId=&tipo=&dias= | Histórico de movimentações | logado |
| GET | /api/painel | Indicadores do painel | logado |
