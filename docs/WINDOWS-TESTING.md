# Verificação Windows

22/10/2026. Ambiente Windows x64; SDK .NET 10 instalado apenas na pasta ignorada `.tools/` para esta implementação.

- Build WPF Release: zero avisos e zero erros.
- 78 verificações automatizadas de modelo, limites UTF-8/PNG/24 MB, fixação, busca sem acentos, filtros, limpeza, duplicatas, pausa, marcações de confidencialidade, DPAPI, persistência mista e rejeição de arquivos inválidos ou grandes.
- Integração do serviço de captura com transporte simulado: não importar conteúdo inicial, texto Unicode, prioridade de imagens, privacidade antes de decodificar, arquivos ignorados, pausa/retomada, um worker por vez, descarte de resultados antigos, captura da próxima cópia, invalidação, falhas de codec, tentativas limitadas quando o clipboard está ocupado e fechamento de cada leitura.
- Três verificações de interface em cada tema: abertura com quatro itens fictícios, busca e filtro Imagens.
- Renderização local de claro/escuro para inspeção visual em `build/windows-preview/`.
- Total: **84 verificações** (78 de modelo/codec/privacidade/persistência/serviço e seis de interface).
- Pacotes portáteis 1.4.0 x64 e ARM64 compilados com runtime incluído, integridade dos ZIPs e SHA-256 conferidos.
- Executável x64 publicado iniciado com `--demo --smoke-test`: abertura e encerramento automático sem erro. Isso não equivale a testar captura ou colagem no clipboard real.
- YAML da CI validado localmente; execução remota e publicação não confirmadas, pois não há conta GitHub autenticada no ambiente.

Os testes usam apenas dados sintéticos e arquivos temporários próprios. Não alteram o clipboard do usuário, não registram atalhos globais, não habilitam login e não substituem aplicativo instalado.

Ainda exige teste manual em ambiente de uso: captura real de diferentes aplicativos, Alt + V, conflito de atalho, menu de bandeja, retorno de foco e colagem de texto/imagem, campos protegidos e aplicativos elevados, login real, múltiplos monitores/DPI e tecnologias assistivas. ARM64 é alvo de compilação, sem execução em hardware ARM64. macOS não foi executado nesta máquina Windows; suas fontes não foram modificadas.
