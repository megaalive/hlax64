const vscode = require('vscode');
const path = require('path');
const { LanguageClient, TransportKind } = require('vscode-languageclient/node');

/** @type {LanguageClient | undefined} */
let client;

function registerVirtualDocCommands(context) {
  const commands = [
    ['hla64.showIr', 'Show IR'],
    ['hla64.showNasm', 'Show NASM'],
    ['hla64.showStackLayout', 'Show stack layout'],
  ];

  for (const [command, title] of commands) {
    context.subscriptions.push(
      vscode.commands.registerCommand(command, async () => {
        const editor = vscode.window.activeTextEditor;
        if (!editor || editor.document.languageId !== 'hla64') {
          vscode.window.showWarningMessage('Open a .hla64 file first.');
          return;
        }

        if (!client) return;
        const uri = editor.document.uri.toString();
        const result = await client.sendRequest('workspace/executeCommand', {
          command,
          arguments: [uri],
        });

        if (result && result.content) {
          const doc = await vscode.workspace.openTextDocument({
            content: result.content,
            language: 'plaintext',
          });
          await vscode.window.showTextDocument(doc, { preview: true, viewColumn: vscode.ViewColumn.Beside });
          vscode.window.setStatusBarMessage(`${title} (read-only virtual document)`, 3000);
        }
      }),
    );
  }
}

/**
 * @param {vscode.ExtensionContext} context
 */
function activate(context) {
  const config = vscode.workspace.getConfiguration('hla64');
  const command = config.get('languageServerPath', 'dotnet');
  const defaultArgs = [
    'run',
    '--project',
    path.join(context.extensionPath, '..', '..', 'src', 'HlaX64.LanguageServer', 'HlaX64.LanguageServer.csproj'),
  ];
  const args = config.get('languageServerArgs', defaultArgs);

  const serverOptions = {
    run: { command, args, transport: TransportKind.stdio },
    debug: { command, args, transport: TransportKind.stdio },
  };

  const clientOptions = {
    documentSelector: [{ scheme: 'file', language: 'hla64' }],
    synchronize: {
      configurationSection: 'hla64',
    },
  };

  client = new LanguageClient('hla64', 'HlaX64 Language Server', serverOptions, clientOptions);
  context.subscriptions.push(client.start());
  registerVirtualDocCommands(context);
}

function deactivate() {
  return client?.stop();
}

module.exports = { activate, deactivate };
