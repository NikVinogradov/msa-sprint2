Команды для разворачивания сервиса в K8s

переключение на репозиторий Docker внутри Minikube.
Результаты команды  docker images будут разными до и после выполнения этой команды.
Но наверное лучше использовать `minikube image load`
```shell
eval $(minikube docker-env)
```
Отключить
```shell
eval $(minikube docker-env -u)
```

Проверить образ в minikube
```shell
minikube image list | grep task4
```
Это не то же самое, что
```shell
docker images | grep task4
```

Сборка образа
```shell
docker build -t task4-booking-service ./booking-service
```

Запустить образ локально в Docker
```shell
docker run --rm -it -p 8081:8080 --name task4-booking-service task4-booking-service:latest 
```

Создание пространства имен в k8s
```shell
kubectl create namespace sprint2-task4
```

Проверка(--dry-run) возможности установки
```shell
helm install sprint2-task4 ./helm/booking-service --debug --dry-run
```

Установка
```shell
helm install sprint2-task4 ./helm/booking-service --debug
```

Обновление
```shell
helm upgrade sprint2-task4 ./helm/booking-service --debug
```

Обновление для staging версии
```shell
helm upgrade sprint2-task4 ./helm/booking-service --debug --values ./helm/booking-service/values-staging.yaml
```