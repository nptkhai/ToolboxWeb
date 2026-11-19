// Builds the page the code playground runs (Content/js/code-playground.js): the user's HTML,
// CSS and JS plus the chosen libraries, as one HTML document for a sandboxed frame (srcdoc),
// the "Open in a new tab" page and the downloaded file.
//
// With a bridge, a small script goes first in <head>: it forwards console calls, uncaught errors,
// rejected promises and resources that fail to load to the playground with postMessage, tagged
// with a per-run token. The frame has no allow-same-origin, so this message is the only way
// out; the playground checks the frame and the token before it shows anything.
(function (window) {
    'use strict';

    // Keeps "</script>" or "</style>" typed by the user from closing our own tag early.
    function guard(text, tag) {
        return String(text || '').replace(new RegExp('</(' + tag + ')', 'gi'), '<\\/$1');
    }

    function escapeHtml(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;');
    }

    // The bridge runs inside the frame, so it is written as plain ES5 text.
    function bridgeScript(token, origin) {
        return '<script>(function(){'
            + 'var T=' + JSON.stringify(token) + ',O=' + JSON.stringify(origin) + ';'
            + 'function post(m){m.__tbxPlayground=T;try{parent.postMessage(m,O);}catch(e){}}'
            + 'function show(v,d,seen){d=d||0;seen=seen||[];var t=typeof v;'
            + 'if(v===null)return"null";if(v===undefined)return"undefined";'
            + 'if(t==="string")return d?JSON.stringify(v):v;'
            + 'if(t==="number"||t==="boolean"||t==="bigint")return String(v);'
            + 'if(t==="symbol")return v.toString();'
            + 'if(t==="function")return"\\u0192 "+(v.name||"anonymous")+"()";'
            + 'if(v instanceof Error)return v.name+": "+v.message;'
            + 'if(typeof Element!=="undefined"&&v instanceof Element){var c=typeof v.className==="string"&&v.className.trim()?"."+v.className.trim().split(/\\s+/).join("."):"";return"<"+v.tagName.toLowerCase()+(v.id?"#"+v.id:"")+c+">";}'
            + 'if(v instanceof Date)return isNaN(v)?"Invalid Date":v.toISOString();'
            + 'if(seen.indexOf(v)>=0)return"[Circular]";'
            + 'if(d>2)return Array.isArray(v)?"[\\u2026]":"{\\u2026}";'
            + 'seen=seen.concat([v]);'
            + 'try{if(Array.isArray(v)){var a=v.slice(0,50).map(function(x){return show(x,d+1,seen);});if(v.length>50)a.push("\\u2026 +"+(v.length-50));return"["+a.join(", ")+"]";}'
            + 'if(typeof Map!=="undefined"&&v instanceof Map){var m=[];v.forEach(function(x,k){if(m.length<50)m.push(show(k,d+1,seen)+" => "+show(x,d+1,seen));});return"Map("+v.size+") {"+m.join(", ")+"}";}'
            + 'if(typeof Set!=="undefined"&&v instanceof Set){var s=[];v.forEach(function(x){if(s.length<50)s.push(show(x,d+1,seen));});return"Set("+v.size+") {"+s.join(", ")+"}";}'
            + 'var keys=Object.keys(v),n=v.constructor&&v.constructor.name&&v.constructor.name!=="Object"?v.constructor.name+" ":"";'
            + 'return n+"{"+keys.slice(0,50).map(function(k){return k+": "+show(v[k],d+1,seen);}).join(", ")+(keys.length>50?", \\u2026":"")+"}";'
            + '}catch(e){return String(v);}}'
            + '["log","info","warn","error","debug"].forEach(function(l){var o=console[l];console[l]=function(){var a=[].slice.call(arguments);'
            + 'post({type:"log",level:l,text:a.map(function(x){return show(x);}).join(" ")});if(o)o.apply(console,a);};});'
            + 'console.table=function(x){console.log(x);};'
            + 'console.clear=function(){post({type:"clear"});};'
            + 'window.addEventListener("error",function(e){var el=e.target;'
            + 'if(el&&el!==window&&(el.src||el.href)){post({type:"log",level:"error",resource:el.src||el.href});return;}'
            + 'var f=e.filename||"";post({type:"log",level:"error",text:e.message||"Error",line:e.lineno||0,col:e.colno||0,file:f.indexOf("srcdoc")>=0?"":f});},true);'
            + 'window.addEventListener("unhandledrejection",function(e){post({type:"log",level:"error",text:"Uncaught (in promise) "+show(e.reason)});});'
            + '})();</' + 'script>';
    }

    function libraryTags(libs) {
        return (libs || []).map(function (lib) {
            if (lib.kind === 'css') {
                return lib.text !== undefined
                    ? '<style>\n' + guard(lib.text, 'style') + '\n</style>'
                    : '<link rel="stylesheet" href="' + escapeHtml(lib.url) + '">';
            }
            return lib.text !== undefined
                ? '<script>\n' + guard(lib.text, 'script') + '\n</' + 'script>'
                : '<script src="' + escapeHtml(lib.url) + '"></' + 'script>';
        }).join('\n');
    }

    function countLines(text) {
        return text ? text.split('\n').length : 0;
    }

    function linesBefore(doc, index) {
        let count = 0;
        for (let i = 0; i < index; i++) {
            if (doc.charCodeAt(i) === 10) count++;
        }
        return count;
    }

    /**
     * build({ html, css, js, title, libs: [{ kind: 'css'|'js', url } | { kind, text }], bridge: { token, origin } | null })
     * → { doc, html: { start, lines } | null, js: { start, lines } }
     * start is the document line where the tab's first line sits, so an error at document line N
     * is line N - start + 1 of that tab.
     */
    function build(options) {
        const html = String(options.html || '');
        const css = String(options.css || '');
        const js = String(options.js || '');
        const head = [
            '<meta charset="utf-8">',
            '<meta name="viewport" content="width=device-width, initial-scale=1">',
            options.title ? '<title>' + escapeHtml(options.title) + '</title>' : '',
            options.bridge ? bridgeScript(options.bridge.token, options.bridge.origin) : '',
            libraryTags(options.libs),
            css ? '<style>\n' + guard(css, 'style') + '\n</style>' : ''
        ].filter(Boolean).join('\n');
        const JS_MARK = '\u0000tbx-js\u0000';
        const HTML_MARK = '\u0000tbx-html\u0000';
        const tail = js ? '<script>\n' + JS_MARK + '\n</' + 'script>' : '';

        let doc;
        // A whole page pasted into the HTML tab keeps its own <html>, <head> and <body>.
        if (/<!doctype|<html[\s>]/i.test(html)) {
            let page = html;
            const headOpen = /<head(\s[^>]*)?>/i.exec(page);
            if (headOpen) {
                const at = headOpen.index + headOpen[0].length;
                page = page.slice(0, at) + '\n' + head + '\n' + page.slice(at);
            } else {
                const htmlOpen = /<html(\s[^>]*)?>/i.exec(page);
                const at = htmlOpen ? htmlOpen.index + htmlOpen[0].length : 0;
                page = page.slice(0, at) + '\n<head>\n' + head + '\n</head>\n' + page.slice(at);
            }
            const bodyClose = page.toLowerCase().lastIndexOf('</body>');
            page = bodyClose >= 0 ? page.slice(0, bodyClose) + tail + '\n' + page.slice(bodyClose) : page + '\n' + tail;
            doc = page;
        } else {
            doc = '<!DOCTYPE html>\n<html>\n<head>\n' + head + '\n</head>\n<body>\n' + HTML_MARK + '\n' + tail + '\n</body>\n</html>';
        }

        let htmlRange = null;
        const htmlAt = doc.indexOf(HTML_MARK);
        if (htmlAt >= 0) {
            htmlRange = { start: linesBefore(doc, htmlAt) + 1, lines: countLines(html) };
            doc = doc.slice(0, htmlAt) + html + doc.slice(htmlAt + HTML_MARK.length);
        }

        let jsRange = { start: 0, lines: 0 };
        const jsAt = doc.indexOf(JS_MARK);
        if (jsAt >= 0) {
            jsRange = { start: linesBefore(doc, jsAt) + 1, lines: countLines(js) };
            doc = doc.slice(0, jsAt) + guard(js, 'script') + doc.slice(jsAt + JS_MARK.length);
        }

        return { doc: doc, html: htmlRange, js: jsRange };
    }

    window.ToolboxPlaygroundSandbox = { build: build };
})(window);
