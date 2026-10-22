# Changelog

## 1.4.1 — Acabamento Windows (em preparação)

- Interface Windows redesenhada a partir da composição macOS: painel arredondado, cabeçalho em vidro, busca em cápsula e cinco filtros segmentados.
- Cartões compactos com ícones vetoriais, miniaturas proporcionais, metadados e indicador de fixação; barra de ações arredondada com nomes acessíveis e foco de teclado.
- Desktop Acrylic pela API pública do DWM no Windows 11 22H2+, com acabamento sólido e gradientes no Windows 10 e sistemas sem transparência. Respeita alto contraste e a opção de transparência do Windows.
- Preferências, prévia, apresentação e confirmação de limpeza com a mesma identidade; controles arredondados, alternadores e aparência segmentada.
- Tipografia suavizada, assets escalados com alta qualidade e prévias PNG em resolução 2×.
- Testes de interface ampliados para ações, estados vazios, privacidade no modo de demonstração, troca de tema, prévias e tamanho mínimo.

## 1.4.0 — Windows e macOS (em preparação)

- Aplicativo nativo WPF/.NET para Windows 10/11 x64 e ARM64, mantendo Swift/AppKit no macOS.
- Histórico de textos, links e imagens PNG/DIB, busca sem acentos, filtros, fixados, prévia, limpeza e cópia em uma linha.
- Alt + V configurável, bandeja, temas, pausa, início no login e colagem automática opcionais.
- Histórico apenas em memória por padrão; persistência opcional criptografada pelo DPAPI do usuário Windows.
- Captura por eventos, processamento serial de imagens e descarte de resultados antigos; limites equivalentes aos do Mac e respeito às marcações de privacidade do Windows.
- Build e ZIP portáteis com runtime incluído, SHA-256 e CI para as duas plataformas.
- Testes Windows sintéticos e renderização nos dois temas; fontes macOS preservadas.

## 1.3.0 — Histórico de imagens

- Captura raster de PNG, TIFF e JPEG (e formatos ImageIO), priorizando a imagem quando a cópia também contém texto.
- Miniaturas, filtro Imagens, dimensões/tamanho e visualização ampliada.
- Seleção oferece PNG e TIFF; mesmo retorno ao destino e colagem com Acessibilidade, ou Command + V manual.
- Captura decodificada em fila serial, com invalidação quando clipboard/pausa mudam.
- Limites de 16 MP e 8 MB por PNG; orçamento de 24 MB compartilhado; fixação, limpeza, privacidade e persistência opcional para imagens.
- Histórico legado continua legível. Sem OCR, download de URLs, leitura de arquivos ou animação.
- 84 verificações passaram, incluindo integração da captura com pasteboard privado.

## 1.2.1 — Identidade oficial

- Logo oficial aplicada no ícone ICNS, histórico, preferências e apresentação.
- Símbolo monocromático vetorial próprio para a barra de menus, adaptado a tamanhos pequenos.
- Paleta oficial, nome em duas cores e assinatura por kushhoficial preservada.
- Lockups claro/escuro, referência original e origem dos assets incluídos no código.
- Pacote universal recompilado; comportamento de captura e colagem preservado.

## 1.2.0 — Copilume

- Marca funcional independente de nome pessoal: Copilume, “Copie agora. Reencontre depois.”
- Corrigida a navegação: histórico, preferências, apresentação e prévia ocultam as outras telas antes de abrir. O atalho só fecha o histórico quando ele é a janela ativa.
- Rastreamento do último aplicativo externo ativo para o destino da colagem.
- Cabeçalho e controles em vidro nativo, fundo sutil com cores, aparência clara/escura/do sistema e respeito à redução de transparência do macOS.
- Menu “Conheça o Copilume” explica a proposta, atalhos, privacidade e instalação pelo DMG.
- Fixar/desafixar textos; filtros Todos, Fixados, Links e Textos.
- Prévia completa e selecionável; fechar a prévia retorna ao histórico.
- Menu de contexto: copiar sem colar, copiar em uma linha, fixar, visualizar e excluir.
- Limpar recentes preserva fixados; a limpeza total pede escolha explícita.
- Compatibilidade de leitura com arquivos salvos sem o campo de fixação.
- Modo de prévia com dados fictícios e sem captura/hotkey/colagem automática, para revisão visual isolada.

## 1.1.0

Base de texto local com busca, histórico em memória por padrão, persistência opcional, filtros de conteúdo confidencial, atalho global configurável e instalador universal.
