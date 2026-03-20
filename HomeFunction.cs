using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using System.Net;

namespace DrowTranslatascan
{
    /// <summary>
    /// Azure Function that serves the themed web UI for the translator.
    /// Responds to <c>GET /api/Home</c> with a self-contained HTML page.
    /// All text and colours are driven by <see cref="Program.Config"/>.
    /// </summary>
    public class HomeFunction
    {
        /// <summary>Returns the translator web UI as an HTML page.</summary>
        [Function("Home")]
        [OpenApiOperation(operationId: "Home", tags: new[] { "ui" }, Summary = "Translator web UI")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/html", bodyType: typeof(string), Description = "HTML page")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "Home")] HttpRequestData req,
            FunctionContext executionContext)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/html; charset=utf-8");
            await response.WriteStringAsync(BuildHtml());
            return response;
        }

        /// <summary>Builds the complete HTML page using values from the language config.</summary>
        internal static string BuildHtml()
        {
            var c = Program.Config;
            var ui = c.Ui;

            // Using $$""" so single { } are literal (for CSS/JS) and {{ }} are interpolation holes.
            return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="UTF-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <title>{{Escape(c.ProjectTitle)}}</title>
              <style>
                *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }

                body {
                  background: {{ui.BackgroundColor}};
                  color: {{ui.TextColor}};
                  font-family: 'Georgia', serif;
                  min-height: 100vh;
                  display: flex;
                  flex-direction: column;
                  align-items: center;
                  padding: 2.5rem 1rem 4rem;
                }

                header {
                  text-align: center;
                  margin-bottom: 2.5rem;
                }

                header h1 {
                  font-size: 2.2rem;
                  letter-spacing: 0.08em;
                  color: {{ui.PrimaryColor}};
                  text-shadow: 0 0 18px {{ui.PrimaryGlow}};
                }

                header p {
                  margin-top: 0.5rem;
                  font-size: 0.95rem;
                  color: {{ui.MutedTextColor}};
                  font-style: italic;
                }

                .card {
                  background: {{ui.CardBackground}};
                  border: 1px solid {{ui.BorderColor}};
                  border-radius: 8px;
                  padding: 2rem;
                  width: 100%;
                  max-width: 680px;
                  box-shadow: 0 4px 32px #00000080;
                }

                label {
                  display: block;
                  font-size: 0.8rem;
                  letter-spacing: 0.1em;
                  text-transform: uppercase;
                  color: #7a6fa0;
                  margin-bottom: 0.4rem;
                }

                textarea {
                  width: 100%;
                  height: 120px;
                  background: {{ui.InputBackground}};
                  border: 1px solid {{ui.InputBorder}};
                  border-radius: 5px;
                  color: #d4cbb8;
                  font-size: 1rem;
                  font-family: inherit;
                  padding: 0.75rem;
                  resize: vertical;
                  outline: none;
                  transition: border-color 0.2s;
                }

                textarea:focus { border-color: #6a44a0; }

                .controls {
                  display: flex;
                  align-items: center;
                  gap: 1rem;
                  margin: 1rem 0;
                  flex-wrap: wrap;
                }

                .direction-group {
                  display: flex;
                  gap: 0.5rem;
                }

                .dir-btn {
                  background: #1c1830;
                  border: 1px solid #3a2e60;
                  border-radius: 4px;
                  color: #9080b8;
                  font-family: inherit;
                  font-size: 0.85rem;
                  padding: 0.45rem 0.9rem;
                  cursor: pointer;
                  transition: background 0.15s, color 0.15s;
                }

                .dir-btn.active, .dir-btn:hover {
                  background: #3a2a60;
                  color: #c8aaee;
                  border-color: #6a44a0;
                }

                .translate-btn {
                  margin-left: auto;
                  background: {{ui.AccentColor}};
                  border: none;
                  border-radius: 5px;
                  color: #e0d0ff;
                  font-family: inherit;
                  font-size: 1rem;
                  padding: 0.55rem 1.6rem;
                  cursor: pointer;
                  letter-spacing: 0.05em;
                  transition: background 0.15s;
                }

                .translate-btn:hover { background: {{ui.AccentHover}}; }
                .translate-btn:disabled { background: #2a1a40; color: #5a4a70; cursor: default; }

                .output-section { margin-top: 1.5rem; }

                .output-box {
                  width: 100%;
                  min-height: 90px;
                  background: {{ui.InputBackground}};
                  border: 1px solid {{ui.InputBorder}};
                  border-radius: 5px;
                  color: #d4cbb8;
                  font-size: 1rem;
                  font-family: inherit;
                  padding: 0.75rem;
                  white-space: pre-wrap;
                  word-break: break-word;
                }

                .output-box.empty { color: #3d3550; font-style: italic; }
                .output-box.error { color: #c06070; border-color: #602030; }

                footer {
                  margin-top: 2.5rem;
                  font-size: 0.78rem;
                  color: #3d3550;
                  text-align: center;
                }

                footer a { color: #5a4a80; text-decoration: none; }
                footer a:hover { color: {{ui.PrimaryColor}}; }
              </style>
            </head>
            <body>
              <header>
                <h1>{{Escape(c.ProjectTitle)}}</h1>
                <p>{{Escape(c.Subtitle)}}</p>
              </header>

              <div class="card">
                <label for="input-text">Input text</label>
                <textarea id="input-text" placeholder="Enter text to translate…" autofocus></textarea>

                <div class="controls">
                  <div class="direction-group" role="group" aria-label="Translation direction">
                    <button class="dir-btn active" id="btn-to-conlang" onclick="setDir('conlang')">
                      {{Escape(c.CommonName)}} &#8594; {{Escape(c.LanguageName)}}
                    </button>
                    <button class="dir-btn" id="btn-to-common" onclick="setDir('common')">
                      {{Escape(c.LanguageName)}} &#8594; {{Escape(c.CommonName)}}
                    </button>
                  </div>
                  <button class="translate-btn" id="translate-btn" onclick="doTranslate()">Translate</button>
                </div>

                <div class="output-section">
                  <label>Translation</label>
                  <div class="output-box empty" id="output">Translation will appear here…</div>
                </div>
              </div>

              <footer>
                <a href="/api/swagger/ui" target="_blank">API docs (Swagger)</a>
                &nbsp;·&nbsp;
                <a href="/api/openapi/v3.json" target="_blank">OpenAPI spec</a>
                &nbsp;·&nbsp;
                <a href="https://github.com/auroris/DrowTranslatascan">GitHub</a>
              </footer>

              <script>
                const conlangName = '{{EscapeJs(c.LanguageName)}}';
                const commonName = '{{EscapeJs(c.CommonName)}}';
                let targetLang = conlangName;

                function setDir(dir) {
                  targetLang = dir === 'conlang' ? conlangName : commonName;
                  document.getElementById('btn-to-conlang').classList.toggle('active', dir === 'conlang');
                  document.getElementById('btn-to-common').classList.toggle('active', dir === 'common');
                }

                document.getElementById('input-text').addEventListener('keydown', function(e) {
                  if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) doTranslate();
                });

                async function doTranslate() {
                  const text = document.getElementById('input-text').value.trim();
                  const out = document.getElementById('output');
                  const btn = document.getElementById('translate-btn');

                  if (!text) {
                    out.textContent = 'Please enter some text first.';
                    out.className = 'output-box error';
                    return;
                  }

                  btn.disabled = true;
                  out.textContent = 'Translating…';
                  out.className = 'output-box empty';

                  try {
                    const params = new URLSearchParams({ text, lang: targetLang });
                    const res = await fetch('/api/Translate?' + params);
                    const body = await res.text();

                    if (res.ok) {
                      out.textContent = body;
                      out.className = 'output-box';
                    } else {
                      out.textContent = body || 'An error occurred.';
                      out.className = 'output-box error';
                    }
                  } catch (err) {
                    out.textContent = 'Could not reach the server.';
                    out.className = 'output-box error';
                  } finally {
                    btn.disabled = false;
                  }
                }
              </script>
            </body>
            </html>
            """;
        }

        private static string Escape(string s) =>
            System.Net.WebUtility.HtmlEncode(s);

        private static string EscapeJs(string s) =>
            s.Replace("\\", "\\\\").Replace("'", "\\'");
    }
}
