# DynamoDB.Net

Easy to use and performant DynamoDB libary for .NET Core  


## 1.0.0

I created the library back in 2016 as an alternative to the "high level" [AWS SDK DynamoDB library](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/DotNetSDKHighLevel.html) that in my opionion was lacking in developer experience, no high level support for query/scan filters, reflection based (slow) serialization etc.  

Because of lack of time to invest in creating documentation and adding a test suite I haven't gotten around to publish it as open source on GitHub and making the binaries available on NuGet.  

## TODO

- Add XML documention for all public classes and members 
- Add simple "Getting started" guide.

## Run Tests

```sh
# Start local DynamoDB server
docker run -p 8000:8000 amazon/dynamodb-local

# Run tests
dotnet test \
    tests/DynamoDB.Net.Tests.csproj \
    /p:CollectCoverage=true \
    /p:CoverletOutputFormat=cobertura \
    /p:CoverletOutput=.coverage/cobertura.xml

# Create code coverage report
reportgenerator \
    -reporttypes:Html \
    -reports:tests/.coverage/cobertura.xml \
    -targetdir:tests/.coverage \
    -filefilters:'-*.g.cs'

# Open code coverage report
open tests/.coverage/index.html
```
