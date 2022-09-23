FROM mcr.microsoft.com/dotnet/sdk:6.0.301-bullseye-slim AS build

RUN apt-get update

#---------------------------------------------------------------------
# Tools setup
#---------------------------------------------------------------------
RUN dotnet tool install --global dotnet-ef
RUN dotnet tool install --global coverlet.console
RUN dotnet tool install --global dotnet-reportgenerator-globaltool

# sonarcloud
ARG SONAR_PROJECT_KEY=Tech-Fabric_CloudFabric.EventSourcing
ARG SONAR_OGRANIZAION_KEY=tech-fabric
ARG SONAR_HOST_URL=https://sonarcloud.io
ARG SONAR_TOKEN
ARG GITHUB_TOKEN
RUN dotnet tool install --global dotnet-sonarscanner
RUN apt-get update && apt-get install -y openjdk-11-jdk
#sonarcloud

ENV PATH="/root/.dotnet/tools:${PATH}" 

RUN curl -L https://raw.githubusercontent.com/Microsoft/artifacts-credprovider/master/helpers/installcredprovider.sh | sh
#---------------------------------------------------------------------
# /Tools setup
#---------------------------------------------------------------------


#---------------------------------------------------------------------
# Test database setup
#---------------------------------------------------------------------
RUN apt-get install -y postgresql postgresql-client postgresql-contrib

USER postgres

RUN echo "local   all   all               md5" >> /etc/postgresql/13/main/pg_hba.conf &&\
    echo "host    all   all   0.0.0.0/0   md5" >> /etc/postgresql/13/main/pg_hba.conf

RUN echo "listen_addresses='*'" >> /etc/postgresql/13/main/postgresql.conf
RUN service postgresql start \
    && psql --command "CREATE ROLE cloudfabric_eventsourcing_test WITH CREATEDB NOSUPERUSER NOCREATEROLE INHERIT NOREPLICATION CONNECTION LIMIT -1 LOGIN PASSWORD 'cloudfabric_eventsourcing_test';" \
    && psql --command "DROP DATABASE IF EXISTS cloudfabric_eventsourcing_test;" \
    && psql --command "CREATE DATABASE cloudfabric_eventsourcing_test WITH OWNER = cloudfabric_eventsourcing_test ENCODING = 'UTF8' CONNECTION LIMIT = -1;" \
    && psql --command "GRANT ALL ON DATABASE cloudfabric_eventsourcing_test TO postgres;"
#---------------------------------------------------------------------
# /Test database setup
#---------------------------------------------------------------------


USER root
WORKDIR /

#---------------------------------------------------------------------
# Nuget restore 
# !Important: this is a nice hack to avoid package restoration on each docker build step.
# Since we only copy nuget.config and csproj files, docker will not run restore unless nuget.config or csproj files change.
#---------------------------------------------------------------------
#COPY nuget.config /src/nuget.config

COPY CloudFabric.EventSourcing.EventStore/CloudFabric.EventSourcing.EventStore.csproj /src/CloudFabric.EventSourcing.EventStore/CloudFabric.EventSourcing.EventStore.csproj
COPY CloudFabric.EventSourcing.Tests/CloudFabric.EventSourcing.Tests.csproj /src/CloudFabric.EventSourcing.Tests/CloudFabric.EventSourcing.Tests.csproj
COPY CloudFabric.Projections/CloudFabric.Projections.csproj /src/CloudFabric.Projections/CloudFabric.Projections.csproj
COPY CloudFabric.Projections.Attributes/CloudFabric.Projections.Attributes.csproj /src/CloudFabric.Projections.Attributes/CloudFabric.Projections.Attributes.csproj

COPY Implementations/CloudFabric.EventSourcing.EventStore.InMemory/CloudFabric.EventSourcing.EventStore.InMemory.csproj /src/Implementations/CloudFabric.EventSourcing.EventStore.InMemory/CloudFabric.EventSourcing.EventStore.InMemory.csproj
COPY Implementations/CloudFabric.EventSourcing.Tests.InMemory/CloudFabric.EventSourcing.Tests.InMemory.csproj /src/Implementations/CloudFabric.EventSourcing.Tests.InMemory/CloudFabric.EventSourcing.Tests.InMemory.csproj
COPY Implementations/CloudFabric.Projections.InMemory/CloudFabric.Projections.InMemory.csproj /src/Implementations/CloudFabric.Projections.InMemory/CloudFabric.Projections.InMemory.csproj

COPY Implementations/CloudFabric.EventSourcing.EventStore.CosmosDb/CloudFabric.EventSourcing.EventStore.CosmosDb.csproj /src/Implementations/CloudFabric.EventSourcing.EventStore.CosmosDb/CloudFabric.EventSourcing.EventStore.CosmosDb.csproj
COPY Implementations/CloudFabric.EventSourcing.Tests.CosmosDb/CloudFabric.EventSourcing.Tests.CosmosDb.csproj /src/Implementations/CloudFabric.EventSourcing.Tests.CosmosDb/CloudFabric.EventSourcing.Tests.CosmosDb.csproj
COPY Implementations/CloudFabric.Projections.CosmosDb/CloudFabric.Projections.CosmosDb.csproj /src/Implementations/CloudFabric.Projections.CosmosDb/CloudFabric.Projections.CosmosDb.csproj

COPY Implementations/CloudFabric.EventSourcing.EventStore.Postgresql/CloudFabric.EventSourcing.EventStore.Postgresql.csproj /src/Implementations/CloudFabric.EventSourcing.EventStore.Postgresql/CloudFabric.EventSourcing.EventStore.Postgresql.csproj
COPY Implementations/CloudFabric.EventSourcing.Tests.Postgresql/CloudFabric.EventSourcing.Tests.Postgresql.csproj /src/Implementations/CloudFabric.EventSourcing.Tests.Postgresql/CloudFabric.EventSourcing.Tests.Postgresql.csproj
COPY Implementations/CloudFabric.Projections.Postgresql/CloudFabric.Projections.Postgresql.csproj /src/Implementations/CloudFabric.Projections.Postgresql/CloudFabric.Projections.Postgresql.csproj

COPY CloudFabric.EventSourcing.Domain/CloudFabric.EventSourcing.Domain.csproj /src/CloudFabric.EventSourcing.Domain/CloudFabric.EventSourcing.Domain.csproj

#RUN dotnet restore /src/CloudFabric.EventSourcing.Domain/CloudFabric.EventSourcing.Domain.csproj
#---------------------------------------------------------------------
# /Nuget restore 
#---------------------------------------------------------------------

#---------------------------------------------------------------------
# Build artifacts
# 
# you can copy sql migration scripts and coverage report files with following script:
# containerId=$(docker create $(docker images --format='{{.ID}}' | head -1) 
# docker cp $containerId:/artifacts/* ./artifacts
# docker rm -v $containerId
#
# !Important all commands must be run in one layer since we will have to copy them later 
# and we don't want to run copy commands on 4 different layers 
#---------------------------------------------------------------------
COPY /. /src

# Start Sonar Scanner
RUN dotnet sonarscanner begin \
  /k:"$SONAR_PROJECT_KEY" \
  /o:"$SONAR_OGRANIZAION_KEY" \
  /d:sonar.host.url="$SONAR_HOST_URL" \
  /d:sonar.login="$SONAR_TOKEN" \
  /d:sonar.cs.opencover.reportsPaths=/artifacts/tests/*/coverage.opencover.xml

RUN service postgresql start && \
    dotnet test /src/Implementations/CloudFabric.EventSourcing.Tests.Postgresql/CloudFabric.EventSourcing.Tests.Postgresql.csproj --logger trx --results-directory /artifacts/tests --configuration Release --collect:"XPlat Code Coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=json,cobertura,lcov,teamcity,opencover

RUN dotnet test /src/Implementations/CloudFabric.EventSourcing.Tests.InMemory/CloudFabric.EventSourcing.Tests.InMemory.csproj --logger trx --results-directory /artifacts/tests --configuration Release --collect:"XPlat Code Coverage"

ARG COVERAGE_REPORT_GENERATOR_LICENSE
ARG COVERAGE_REPORT_TITLE
ARG COVERAGE_REPORT_TAG
ARG COVERAGE_REPORT_GENERATOR_HISTORY_DIRECTORY

RUN reportgenerator "-reports:/artifacts/tests/*/coverage.cobertura.xml" -targetdir:/artifacts/code-coverage "-reporttypes:HtmlInline_AzurePipelines_Light;SonarQube;TextSummary" "-title:$COVERAGE_REPORT_TITLE" "-tag:$COVERAGE_REPORT_TAG" "-license:$COVERAGE_REPORT_GENERATOR_LICENSE" "-historydir:$COVERAGE_REPORT_GENERATOR_HISTORY_DIRECTORY"

# End Sonar Scanner
RUN dotnet sonarscanner end /d:sonar.login="$SONAR_TOKEN"

RUN dotnet pack /src/CloudFabric.EventSourcing.Domain/CloudFabric.EventSourcing.Domain.csproj -o /artifacts/nugets -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg && \
    dotnet pack /src/CloudFabric.EventSourcing.EventStore/CloudFabric.EventSourcing.EventStore.csproj -o /artifacts/nugets -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg && \
    dotnet pack /src/CloudFabric.Projections/CloudFabric.Projections.csproj -o /artifacts/nugets -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg && \
    dotnet pack /src/CloudFabric.Projections.Attributes/CloudFabric.Projections.Attributes.csproj -o /artifacts/nugets -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg && \
    dotnet pack /src/Implementations/CloudFabric.EventSourcing.EventStore.CosmosDb/CloudFabric.EventSourcing.EventStore.CosmosDb.csproj -o /artifacts/nugets -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg && \
    dotnet pack /src/Implementations/CloudFabric.EventSourcing.EventStore.InMemory/CloudFabric.EventSourcing.EventStore.InMemory.csproj -o /artifacts/nugets -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg && \
    dotnet pack /src/Implementations/CloudFabric.EventSourcing.EventStore.Postgresql/CloudFabric.EventSourcing.EventStore.Postgresql.csproj -o /artifacts/nugets -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg && \
    dotnet pack /src/Implementations/CloudFabric.Projections.CosmosDb/CloudFabric.Projections.CosmosDb.csproj -o /artifacts/nugets -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg && \
    dotnet pack /src/Implementations/CloudFabric.Projections.InMemory/CloudFabric.Projections.InMemory.csproj -o /artifacts/nugets -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg && \
    dotnet pack /src/Implementations/CloudFabric.Projections.Postgresql/CloudFabric.Projections.Postgresql.csproj -o /artifacts/nugets -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg 
#---------------------------------------------------------------------
# /Build artifacts
#---------------------------------------------------------------------
