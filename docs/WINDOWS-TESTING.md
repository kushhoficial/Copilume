# Verificação Windows

22/10/2026. Ambiente Windows x64; SDK .NET 10 instalado apenas na pasta ignorada `.tools/` para esta implementação.

- Build WPF Release: zero avisos e zero erros.
- 78 verificações automatizadas de modelo, limites UTF-8/PNG/24 MB, fixação, busca sem acentos, filtros, limpeza, duplicatas, pausa, marcações de confidencialidade, DPAPI, persistência mista e rejeição de arquivos inválidos ou grandes.
- Integração do serviço de captura com transporte simulado: não importar conteúdo inicial, texto Unicode, prioridade de imagens, privacidade antes de decodificar, arquivos ignorados, pausa/retomada, um worker por vez, descarte de resultados antigos, captura da próxima cópia, invalidação, falhas de codec, tentativas limitadas quando o clipboard está ocupado e fechamento de cada leitura.
- Trinta e duas verificações de interface/integração nativa em cada tema: histórico, busca, filtros, fixação, pausa, opções privadas no modo de demonstração, aparência, prévias, autoria/licença, estados vazios, tamanho mínimo, menus de bandeja/itens/texto, navegação por teclado, retorno ao painel por Esc, posicionamento e contorno Win32.
- Renderização local em resolução 2× para inspeção visual de histórico, preferências, prévia de imagem, apresentação e painel compacto nos dois temas em `build/windows-preview/`. As imagens mostram o acabamento sólido do aplicativo: o renderizador WPF não captura o Acrylic do DWM.
- Total: **142 verificações** (78 de modelo/codec/privacidade/persistência/serviço e 64 de interface/integração nativa).
- Interface 1.4.2 com composição própria WPF, ícones vetoriais e Desktop Acrylic via DWM em Windows 11 22H2+, com fallback sólido e respeito à transparência do sistema/alto contraste.
- Região nativa consultada com `GetWindowRgn`/`PtInRegion`: os quatro cantos são excluídos, o centro permanece e o recorte sobrevive ao resize e à troca de tema. `GetClientRect` e `GetWindowRect` confirmam que não existe faixa reservada pela moldura atrás do vidro. `WM_NCHITTEST` confirma resize pela borda.
- Menus de bandeja e de item usam as mesmas janelas em vidro: formas nativas redondas, callback de pausa, ação de texto desabilitada para imagem, setas pulando ações desabilitadas, Esc sem executar ação e posicionamento acima da barra de tarefas/monitores negativos. Menus de texto somente leitura bloqueiam corte e colagem.
- Inspeção na sessão gráfica com Computer Use, somente `--demo`: painel, abertura do menu do item e abertura do menu de edição da busca. O fundo e a moldura foram conferidos ao abrir o menu e perder foco. O caminho pelo overflow da bandeja do Explorer ainda requer validação manual; o componente e callbacks usados pela bandeja estão cobertos pela suíte.
- Resultados anteriores: ZIPs 1.4.1 x64/ARM64 e seus SHA-256 conferidos; executável 1.4.1 x64 aberto com `--demo --dark --smoke-test` e encerrado sem erro. A verificação do pacote 1.4.2 é registrada após sua geração.
- YAML da CI validado localmente; execução remota e publicação não confirmadas, pois não há conta GitHub autenticada no ambiente.

Os testes usam apenas dados sintéticos e arquivos temporários próprios. Não alteram o clipboard do usuário, não registram atalhos globais, não habilitam login e não substituem aplicativo instalado.

Ainda exige teste manual em ambiente de uso: captura real de diferentes aplicativos, Alt + V, conflito de atalho, menu de bandeja, retorno de foco e colagem de texto/imagem, campos protegidos e aplicativos elevados, login real, múltiplos monitores/DPI e tecnologias assistivas. ARM64 é alvo de compilação, sem execução em hardware ARM64. macOS não foi executado nesta máquina Windows; suas fontes não foram modificadas.

O efeito Acrylic depende do compositor, da versão do Windows e do fundo. Os renders automatizados verificam o desenho e o fallback, sem certificar o efeito sobre todas as configurações do Windows. A inspeção gráfica acima não substitui testes em Windows 10, ARM64 físico, alto contraste ou múltiplos monitores com DPI diferentes. Nenhuma instância pessoal foi encerrada ou substituída; as prévias usaram exclusivamente dados fictícios.
