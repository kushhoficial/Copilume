# Verificação Windows

22/10/2026. Ambiente Windows x64; SDK .NET 10 instalado apenas na pasta ignorada `.tools/` para esta implementação.

- Build WPF Release: zero avisos e zero erros.
- 78 verificações automatizadas de modelo, limites UTF-8/PNG/24 MB, fixação, busca sem acentos, filtros, limpeza, duplicatas, pausa, marcações de confidencialidade, DPAPI, persistência mista e rejeição de arquivos inválidos ou grandes.
- Integração do serviço de captura com transporte simulado: não importar conteúdo inicial, texto Unicode, prioridade de imagens, privacidade antes de decodificar, arquivos ignorados, pausa/retomada, um worker por vez, descarte de resultados antigos, captura da próxima cópia, invalidação, falhas de codec, tentativas limitadas quando o clipboard está ocupado e fechamento de cada leitura.
- Quatorze verificações de interface em cada tema: abertura com quatro itens fictícios, busca, filtro Imagens, fixar/desafixar via barra e filtros, pausa/retomada, opções privadas indisponíveis no modo de demonstração, aparência ao vivo, prévias com resolução e texto originais, autoria/licença, estado vazio e tamanho mínimo.
- Renderização local em resolução 2× para inspeção visual de histórico, preferências, prévia de imagem, apresentação e painel compacto nos dois temas em `build/windows-preview/`. As imagens mostram o acabamento sólido do aplicativo: o renderizador WPF não captura o Acrylic do DWM.
- Total: **106 verificações** (78 de modelo/codec/privacidade/persistência/serviço e 28 de interface).
- Interface 1.4.1 com composição própria WPF, ícones vetoriais e Desktop Acrylic via DWM em Windows 11 22H2+, com fallback sólido e respeito à transparência do sistema/alto contraste.
- Pacotes portáteis 1.4.1 x64 e ARM64 gerados separadamente por versão, com runtime incluído; integridade dos ZIPs e SHA-256 conferidos.
- Executável portátil x64 1.4.1 iniciado com `--demo --dark --smoke-test`: abertura e encerramento automático sem erro, enquanto a cópia pessoal 1.4.0 continuou em execução. Isso não equivale a testar captura ou colagem no clipboard real.
- YAML da CI validado localmente; execução remota e publicação não confirmadas, pois não há conta GitHub autenticada no ambiente.

Os testes usam apenas dados sintéticos e arquivos temporários próprios. Não alteram o clipboard do usuário, não registram atalhos globais, não habilitam login e não substituem aplicativo instalado.

Ainda exige teste manual em ambiente de uso: captura real de diferentes aplicativos, Alt + V, conflito de atalho, menu de bandeja, retorno de foco e colagem de texto/imagem, campos protegidos e aplicativos elevados, login real, múltiplos monitores/DPI e tecnologias assistivas. ARM64 é alvo de compilação, sem execução em hardware ARM64. macOS não foi executado nesta máquina Windows; suas fontes não foram modificadas.

O efeito Acrylic depende do compositor, da versão do Windows e do fundo; requer inspeção no desktop. Os renders automatizados verificam o desenho e o fallback, sem certificar o efeito sobre todas as configurações do Windows. A cópia 1.4.0 em uso foi preservada; nenhuma instância pessoal foi encerrada ou substituída.
