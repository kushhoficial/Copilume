# Copilume — manutenção

- Este é o projeto oficial de kushhoficial: `kushhoficial/Copilume`.
- O usuário autorizou enviar ao GitHub as atualizações concluídas deste projeto. Após implementar, revisar e passar os checks adequados, crie um commit descritivo e envie à branch apropriada. Confira a CI e corrija falhas relacionadas. Não envie arquivos alheios, credenciais, clipboard, caches ou binários ao Git.
- Não faça push a cada arquivo salvo; publique mudanças coerentes e verificadas. Não use force push nem mova tags já publicadas. Se a conexão exigir uma nova permissão, explique o bloqueio e peça a intervenção necessária.
- Preserve a assinatura kushhoficial, a licença MIT, a identidade oficial e as opções de privacidade desativadas por padrão. Use Swift/AppKit, APIs públicas e poucas dependências.
- Testes: `./test.sh`. Build universal: `./build.sh`. Distribuição: `./package.sh`.
- Atualize VERSION, BUILD_NUMBER, CHANGELOG e notas de versão ao preparar uma nova Release. Tags `vX.Y.Z` acionam a publicação automática após CI; não crie uma Release para toda alteração pequena.
- Não substitua nem encerre a cópia do aplicativo em uso sem considerar a perda do histórico apenas em memória. Para testes visuais, use uma cópia isolada em modo `--demo`; encerre e remova as cópias de teste ao concluir.
- Nunca desative proteções do macOS ou contorne Acessibilidade. Não registre conteúdo real do clipboard em logs.
