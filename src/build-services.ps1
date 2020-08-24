docker build -t kellybirr/tracing-demo:grpc .\grpc\
docker push kellybirr/tracing-demo:grpc

docker build -t kellybirr/tracing-demo:webapi .\webapi/
docker push kellybirr/tracing-demo:webapi
