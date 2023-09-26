# DynamoDB.Net

Easy to use and performant DynamoDB libary for .NET Core  


## 1.0.0-beta

I created the library back in 2016 as an alternative to the "high level" [AWS SDK DynamoDB library](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/DotNetSDKHighLevel.html) that in my opionion had quite bad developer experience, no high level support for query/scan filters, reflection based (slow) serialization etc. I has since been used in both commercial and non-commercial products.  

Because of lack of time to invest in creating documentation and adding a test suite I haven't gotten around to publish it as open source on GitHub and making the binaries available on NuGet.  

At the time when this library was created `Newtonsoft.Json` was the defacto JSON serializer for .NET Core so it was chosen as the sole option for object serialization. As `Newtonsoft.Json` has been replaced by `System.Text.Json` and is no longer an essential part of the .NET ecosystem I would want to make it an optional serialization method instead (for backward compatibility) before I release a proper `1.0.0` version of this package. 
There is also missing functionality like transaction support which has been added to DynamoDB after this library was created.

### TODO

* Write acceptance test suite
* Extract all `Newtonsoft.Json` related serialization functionality to it's own package
* Implement alternative serialization method
* Write unit tests to increase code coverage
* Add support for transactions


## Run Tests

```sh
# Start local DynamoDB server
docker run -p 8000:8000 amazon/dynamodb-local

# Run tests
dotnet test \
    tests/DynamoDB.Net.Tests.csproj \
    --logger:"console;verbosity=detailed" \
    /p:CollectCoverage=true \
    /p:CoverletOutputFormat=cobertura \
    /p:CoverletOutput=.coverage/cobertura.xml

# Create code coverage report
reportgenerator \
    -reporttypes:Html \
    -reports:tests/.coverage/cobertura.xml \
    -targetdir:tests/.coverage

# Open code coverage report
open tests/.coverage/index.html
```
