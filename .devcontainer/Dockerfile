FROM mcr.microsoft.com/devcontainers/dotnet:10.0-noble

USER vscode

RUN dotnet tool install --global paket \
    && dotnet tool install --global Fun.Build.Cli \
    && dotnet tool install --global fantomas

USER root

RUN curl -fsSL https://deb.nodesource.com/setup_lts.x | bash - \
    && apt-get install -y nodejs

COPY .devcontainer/newf.sh /usr/local/bin/newf
RUN chmod +x /usr/local/bin/newf

COPY .devcontainer/bundlef.sh /usr/local/bin/bundlef
RUN chmod +x /usr/local/bin/bundlef

COPY .devcontainer/runf.sh /usr/local/bin/runf
RUN chmod +x /usr/local/bin/runf

COPY .devcontainer/watchf.sh /usr/local/bin/watchf
RUN chmod +x /usr/local/bin/watchf

COPY .devcontainer/replf.sh /usr/local/bin/replf
RUN chmod +x /usr/local/bin/replf
