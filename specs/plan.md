# Plano técnico

## Contexto
Implementa a especificação do encurtador de links, respeitando a constituição
do projeto.

## Arquitetura
- API HTTP em Minimal API, um arquivo de endpoints por recurso
- Camada de aplicação com LinkService, dona das regras RN-01 a RN-06
- Camada de dados com EF Core e um repositório por agregado
- Endpoint só valida entrada, chama o serviço e traduz o resultado em resposta

## Decisões
- D-01: SQLite como armazenamento, porque o RF-08 exige persistência
  entre reinícios
- D-02: contador incrementado por UPDATE atômico no banco, e não lido e
  reescrito na aplicação, para atender a RN-04 sob acesso concorrente
- D-03: código automático tem 7 caracteres e usa somente A-Z e os dígitos
  de 1 a 9; código personalizado aceita de 4 a 20 caracteres do mesmo alfabeto
- D-04: unicidade do código garantida por índice único no banco, além da
  checagem no serviço, porque a checagem sozinha tem corrida; colisão em código
  automático gera retry, enquanto código personalizado duplicado é recusado

## Modelo de dados
Link
- Id, Code (único), TargetUrl, CreatedAtUtc, ExpiresAtUtc (nulo), AccessCount

## Contratos
- POST /api/links entrada: targetUrl, code opcional, expiresAtUtc opcional;
  sem expiresAtUtc, o link permanece válido sem prazo; se informada, a data é
  UTC e o link permanece válido até o fim desse dia UTC; data passada é recusada
  com 400 ProblemDetails
- POST /api/links saída: shortUrl e code
- GET /{code} incrementa o contador atomicamente e redireciona com 302 se válido;
  responde 404 se não existe ou 410 se expirou, sem incrementar o contador
- GET /api/links/{code}/stats retorna publicamente o total de acessos para
  qualquer pessoa que conheça o código, sem chave de gerenciamento

## Riscos
- Concorrência na criação: o índice único é a garantia final; códigos
  automáticos tentam novamente em caso de colisão e códigos próprios duplicados
  são recusados
- Crescimento da tabela de links sem política de limpeza, fora de escopo por ora