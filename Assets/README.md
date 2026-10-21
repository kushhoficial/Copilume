# Identidade oficial do Copilume

Referência aprovada e fornecida por kushhoficial: `OfficialBrandBoard.png`, preservada sem alterações. O crédito do produto continua **por kushhoficial**; documentação e licença mantêm **@kushhoficial**.

## Arquivos de produção

- `AppIcon.png`: ícone isolado com transparência, 1254 × 1254. Derivado da referência com a ferramenta integrada de geração de imagens (edição com referência), para separar o símbolo da prancha. Não é um recorte pixel a pixel. Não há geração de imagens ou IA em tempo de execução no aplicativo.
- `MenuIconTemplate.svg`: adaptação óptica monocromática das três folhas e seta de retorno para 16–22 px. A geometria equivalente está em `Sources/Design.swift` e gera o PDF vetorial template no build. O macOS controla sua cor conforme a barra de menus.
- `LockupLight.svg` e `LockupDark.svg`: composições autocontidas para documentação, com ícone incorporado e tipografia do sistema. No app o nome e o crédito são texto nativo acessível, com SF do macOS.
- `scripts/MakeIcon.swift`: gera todas as resoluções do iconset, o PDF da barra e imagens de inspeção. `build.sh` gera o ICNS e incorpora os recursos no .app. Não baixa dependências.

Paleta: azul profundo #0B2A44, azul-petróleo #0E7490, ciano #38BDF8, gelo #E6F6FF e cinza #94A3BB. As superfícies e o texto secundário usam cores semânticas do macOS para respeitar acessibilidade.

## Instrução usada na edição com referência

Isole apenas o ícone principal grande, no canto superior esquerdo da prancha fornecida, em uma imagem quadrada de alta resolução com fundo transparente. Preserve fielmente o desenho aprovado: base quadrada arredondada azul profundo/azul-petróleo com borda ciano, três folhas sobrepostas em azul e ciano, folha branca frontal inclinada com duas linhas azuis e seta curva ciano apontando para a direita. Preserve proporções, orientação, materiais, luz e cores. Não redesenhe a marca, não adicione elementos, texto, legenda, assinatura, prancha, mockup ou fundo. Centralize com pequena margem transparente e sem cortar a sombra do ícone.

O texto acima documenta a intenção de extração. A referência original permanece incluída para comparação e futura substituição por arquivos vetoriais de origem, caso disponibilizados pelo autor.
