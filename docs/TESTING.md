# Verificação — Copilume 1.3.0

21/10/2026. Apple Silicon arm64, macOS 27.0.1, Swift 6.4 / Command Line Tools. Nenhuma dependência instalada.

## Testado nesta versão

- **84 verificações passaram**: 74 de modelo/codec/clipboard e 10 de integração da captura real `App.poll` em pasteboard privado. Saída em `test-results.txt`. Somente dados sintéticos.
- PNG, TIFF e JPEG de entrada; normalização PNG; dimensões; transparência parcial; orientação EXIF; miniatura; retorno PNG/TIFF sem placeholder de texto; persistência mista e fixação; leitura de histórico legado; limites individuais/agregados; privacidade e pausa.
- Integração: imagem tem prioridade sobre texto acompanhante; resultado do worker chega ao modelo; clipboard sem mudança e imagem repetida não duplicam; marcação confidencial rejeitada antes de decodificar; pausa respeitada; imagem antiga descartada se clipboard mudar; próxima cópia capturada após worker; mudança de época invalida captura; imagem inválida gera aviso.
- Interface isolada `--demo`: miniatura, dimensões, filtro Imagens, visualização ampliada e fechamento de volta ao histórico. Captura `images.png` com dados fictícios.
- **Enter na imagem pela interface** produziu PNG/TIFF de 640 × 360 no clipboard geral. O Pré-Visualização do macOS abriu o resultado com “Novo a partir da Área de Transferência”. O documento sintético foi descartado e o clipboard anterior foi restaurado por um auxiliar com backup apenas em memória, sem log de conteúdo.
- Fluxo sem permissão: aviso “Copiado. Use ⌘V no destino” exibido após selecionar a imagem. A prévia não solicita Acessibilidade nem envia teclas automaticamente.
- Build universal arm64/x86_64, assinatura ad hoc estrita e DMG verificados; pacote remontado somente leitura e recursos comparados com o build; ZIP do fonte verificado.

## Limitações

A cópia instalada em uso não foi substituída ou encerrada. A integração de captura foi testada com pasteboard nativo privado, sem monitorar as cópias pessoais durante os testes. A colagem automática de imagem por Acessibilidade não foi retestada ponta a ponta nesta versão; o acionamento segue o mesmo fluxo anteriormente validado pelo usuário. A nova assinatura ad hoc pode exigir readicionar a autorização do aplicativo instalado pelo painel oficial.

O destino precisa aceitar imagens. Não foram testados todos os navegadores, editores, formatos ou perfis de cor; sem OCR, arquivos ou animação. Somente a primeira imagem/quadro de uma cópia é mantida. Intel foi compilado e ligado, sem execução em hardware Intel; linker local avisa sobre a ausência de x86_64 no arquivo opcional libswiftCompatibilityPacks.a. macOS 13–26 e login real não foram testados.

Pacote sem notarização Apple; nenhuma publicação em GitHub ou envio à Apple foi feito. A identidade e assinatura kushhoficial foram preservadas.

## Histórico das verificações anteriores

# Verificação — Copilume 1.2.1

21/10/2026. Atualização de identidade visual. Build universal arm64/x86_64 concluído e assinatura local verificada. Inspeção em prévia isolada com dados fictícios, sem disputar o atalho ou substituir a cópia em uso.

- Logo e assinatura conferidas no histórico claro/escuro, preferências e apresentação, sem cortes nos tamanhos testados. Capturas atualizadas nesta pasta.
- Retorno de preferências ao histórico pelo comando de menu conferido nos dois temas; apenas o histórico é reaberto.
- Master PNG, ICNS e PDF template incluídos nos recursos assinados. Ícone da barra renderizado e inspecionado em tamanho pequeno.
- DMG universal gerado, verificado e conferido por remontagem somente leitura; fonte ZIP verificada.
- Captura e colagem não foram alteradas nem retestadas nesta atualização visual. Os testes do modelo abaixo pertencem à versão 1.2.0. A colagem física foi confirmada pelo usuário após instalação limpa; a nova compilação ad hoc pode exigir readicionar a autorização oficial do app instalado.
- O pacote continua sem notarização Apple. Execução Intel e macOS anteriores não foram verificadas.

## Verificação anterior — 1.2.0


20/10/2026, São Paulo. Mac Apple Silicon, macOS 27.0.1, Swift 6.4, Command Line Tools. Sem dependências instaladas.

## Confirmado

- Build otimizado universal arm64/x86_64, assinatura ad hoc com hardened runtime e verificação estrita.
- **44 testes automatizados passaram** com dados sintéticos. Resultado em `test-results.txt`.
- Novas verificações do modelo: fixados protegidos no descarte, primeiro descarte de não fixados, filtro de fixados, limpeza que preserva fixados, persistência opcional de fixação, desafixar, classificação de links com espaços externos, filtro de textos, composição de busca/filtros, cópia em uma linha, limite ocupado por fixados e leitura do formato anterior sem campo de fixação.
- Mantidos os testes de privacidade, marcações confidenciais conhecidas, duplicatas, limites de 200 itens/24 MB, persistência opcional, permissões 0700/0600 e rejeição de arquivo inválido.
- Prévia de interface isolada em uma cópia de teste com identificador próprio e dados fictícios. Não registra atalho global, não captura o clipboard, não salva histórico e não envia teclas para outros aplicativos. A versão anterior em uso não foi substituída ou encerrada.
- Filtros de links e fixados mostraram os itens esperados; botão de fixação alterou os resultados do filtro.
- Menu de contexto “Copiar em uma linha” foi acionado; o texto no clipboard foi comparado à string sintética esperada e correspondeu exatamente, sem imprimir o conteúdo real do clipboard.
- Prévia completa mostrou o texto com suas quebras; fechar a prévia voltou ao histórico.
- Abertura de preferências, seleção de aparência escura e retorno ao histórico pelo comando de menu `Abrir histórico` funcionaram. O comando da barra de menus usa o mesmo handler, que agora oculta preferências, apresentação e prévia antes de ativar o painel.
- Telas de histórico claro/escuro, preferências e “Conheça o Copilume” inspecionadas visualmente, sem cortes nos conteúdos testados. O vidro nativo aparece no cabeçalho/filtros e nos controles.
- DMG gerado e validado por checksum interno. Arquivos de distribuição possuem SHA-256 em `dist/SHA256SUMS.txt`.

## Limites

A execução nativa foi testada em Apple Silicon. A fatia Intel foi compilada e ligada, mas não executada em um Mac Intel. O linker local emitiu aviso sobre ausência de fatia x86_64 em `libswiftCompatibilityPacks.a`; a linkedição terminou. Não foram testados macOS 13–26, todos os layouts de teclado, todas as combinações de contraste/transparência, login real nem todos os aplicativos de destino.

A colagem e o atalho físico da versão anterior foram confirmados pelo usuário após a instalação limpa. Nesta iteração, para preservar a instalação funcionando, a revisão de interface usou o modo de prévia, sem registrar outro atalho ou solicitar nova autorização. A identidade nova `io.github.kushhoficial.Copilume` precisará de Acessibilidade própria para colar automaticamente. A colagem ponta a ponta dessa nova identidade deve ser validada pelo usuário ao instalar o novo pacote; o modo de prévia não foi contado como esse teste.

O teste de navegação acionou o menu do aplicativo, cujo comando usa o mesmo handler do menu do status item. O menu do status item não foi acionado por coordenadas globais nesta automação.

O pacote é ad hoc e não notarizado. Nenhuma publicação no GitHub, CI remota ou envio à Apple foi feito. Não houve teste com gerenciador de senhas real; foram testados os tipos de marcação no modelo. Não há garantia de identificar senhas sem marcação.
