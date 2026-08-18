<p align="center">
  <img src="src/docs/assets/fspure.png" alt="fspure logo" width="520" />
</p>

> Typically, interactions with the outside world occur at the boundary of your application.  
> — Isaac Abraham

This project explores how an **F# analyzer** and **VS Code extension** can help you push impurity to the boundary of your application.

![pure / impure decorations in the editor](src/docs/assets/image.png)

## Why should I care?

Effects at the boundary leave you a core that is deterministic: same inputs, same outputs, no hidden I/O or mutation. That code is easier to test, review, and change. Getting there is not a single rewrite. It is a non-deterministic process — find an impure call, decide whether it belongs in the core, push it out, repeat. The analyzer reports what is still impure, the VS Code extension shows it on the function you are looking at, and an AI agent using the fspure skill can do the mechanical rewrites so you spend your time on those decisions.

## How does it work?

It does that by defining a pure subset and marking everything else as impure. The analyzer checks your F# code and labels each definition. The VS Code extension visualizes those labels as end-of-line **pure** / **impure** badges. An agent can use the same analyzer output to rewrite functions toward purity automatically. You can join the ecosystem by shipping purity information for your own libraries, instead of covering only F# core and the BCL.
