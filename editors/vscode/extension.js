const vscode = require('vscode');
const path = require('path');
const { LanguageClient, TransportKind } = require('vscode-languageclient/node');

/** @type {LanguageClient | undefined} */
let client;

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
  };

  client = new LanguageClient('hla64', 'HlaX64 Language Server', serverOptions, clientOptions);
  context.subscriptions.push(client.start());
}

function deactivate() {
  return client?.stop();
}

module.exports = { activate, deactivate };
