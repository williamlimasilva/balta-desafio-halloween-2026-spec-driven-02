# Tarefas

| ID | Tarefa | Origem | Depende de | Concluída quando |
|---|---|---|---|---|
| T-01 | Criar projeto e estrutura de pastas | Constituição | | Projeto compila |
| T-02 | Modelar a entidade Link e a migração | RF-01, RF-08 | T-01 | Migração aplica no banco |
| T-03 | Gerar código aleatório de 7 caracteres | RF-02, D-03 | T-01 | Teste unitário cobre formato e alfabeto |
| T-04 | Validar URL de destino | RN-02, borda 2048 | T-01 | Testes unitários cobrem http, https, URLs inválidas e limite de 2048 caracteres |
| T-05 | Criar link com código automático e expiração opcional | RF-01, RF-02, RF-06, RN-06, D-03, D-04 | T-02, T-03, T-04 | Teste de integração cria e retorna 201; teste unitário cobre ausência e rejeição de data passada; data passada retorna 400 ProblemDetails; teste unitário confirma nova tentativa após colisão automática |
| T-06 | Aceitar código próprio com unicidade | RF-03, RN-01, RN-05, D-04 | T-05 | Testes unitários cobrem formato e unicidade; código próprio duplicado retorna 409 |
| T-07 | Redirecionar por código e aplicar expiração UTC | RF-04, RF-06, RN-03 | T-05 | Teste unitário cobre validade até o fim do dia UTC e expiração após esse limite; teste de integração cobre redirect 302 e link expirado 410 sem incrementar o contador |
| T-08 | Incrementar contador de forma atômica | RF-05, RN-04, D-02 | T-07 | Testes unitários confirmam que o contador não diminui; teste concorrente não perde contagem |
| T-09 | Consultar estatísticas públicas por código | RF-07 | T-08 | Teste de integração consulta o total de acessos sem chave de gerenciamento e confirma o contador atualizado por T-08 |