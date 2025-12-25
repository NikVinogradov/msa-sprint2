### **Название задачи:** ADR-001 Целевая архитектура Hotelio и миграция к микросервисам (Strangler Fig)
### **Автор:** Николай Виноградов
### **Дата:** 2025-12-24

### Текущие сервисы в монолите

### Контекст пробемы

Проект **Hotelio** начал с простого монолита, но с ростом бизнеса возникли серьёзные архитектурные и операционные проблемы. Команда приняла решение о переходе на микросервисную архитектуру с постепенным выносом сервисов.

### Текущие проблемы
Сложность сопровождения
    Любое изменение требует понимания всей кодовой базы.
    Невозможно менять один модуль, не затрагивая другие.
Низкая масштабируемость
    При пиковых нагрузках (например, на бронирование) нельзя масштабировать только нужный компонент.
    Производительность неравномерна.
Невозможность гибкой разработки
    Разные команды не могут работать независимо.
    Трудно внедрить быстрый CI/CD.
Ограничения на фронтенде
    REST API монолита слишком универсален и плохо адаптируется под разные платформы.
    Нет поддержки BFF.
Сложности с запуском новых фич
    Большой риск ошибок из-за плотной связанности.
    Тестирование требует понимания всех зависимостей.

#### Текущая структура монолита
```
@startuml
!includeurl https://raw.githubusercontent.com/plantuml-stdlib/C4-PlantUML/master/C4_Component.puml

LAYOUT_WITH_LEGEND()

System_Boundary(hotelio, "Hotelio Monolith") {

  Container(monolith, "Monolith Application", "Spring Boot", "Legacy app exposing REST endpoints for hotel booking") {

    ' Controllers
    Component(bookingController, "BookingController", "Spring MVC", "/api/bookings")
    Component(hotelController, "HotelController", "Spring MVC", "/api/hotels")
    Component(promoController, "PromoCodeController", "Spring MVC", "/api/promos")
    Component(reviewController, "ReviewController", "Spring MVC", "/api/reviews")
    Component(userController, "UserController", "Spring MVC", "/api/users")

    ' Services
    Component(bookingService, "BookingService", "Java Service", "Validates and creates bookings")
    Component(hotelService, "HotelService", "Java Service", "Retrieves hotel details")
    Component(userService, "UserService", "Java Service", "Validates user status and blacklist")
    Component(promoService, "PromoCodeService", "Java Service", "Applies discounts and rules")
    Component(reviewService, "ReviewService", "Java Service", "Manages hotel reviews")

    ' DB
    ComponentDb(postgres, "Monolith DB", "PostgreSQL", "Stores users, hotels, bookings, reviews, promos")

    ' Controller-Service relations
    Rel(bookingController, bookingService, "Uses")
    Rel(hotelController, hotelService, "Uses")
    Rel(promoController, promoService, "Uses")
    Rel(reviewController, reviewService, "Uses")
    Rel(userController, userService, "Uses")

    ' Service-Service and Service-DB
    Rel(bookingService, hotelService, "Calls")
    Rel(bookingService, userService, "Calls")
    Rel(bookingService, promoService, "Calls")
    Rel(bookingService, reviewService, "Calls")

    Rel(bookingService, postgres, "Reads/Writes")
    Rel(userService, postgres, "Reads")
    Rel(hotelService, postgres, "Reads")
    Rel(promoService, postgres, "Reads")
    Rel(reviewService, postgres, "Reads/Writes")
  }
}

Person(user, "User", "Interacts with frontend")
Rel(user, bookingController, "Uses")
Rel(user, hotelController, "Uses")
Rel(user, promoController, "Uses")
Rel(user, reviewController, "Uses")
Rel(user, userController, "Uses")
@enduml
```

#### Разбиение на микровервисы
```
@startuml
!include https://raw.githubusercontent.com/plantuml-stdlib/C4-PlantUML/master/C4_Container.puml

title Container Diagram - Target State

Person(user, "End user")
Person(partner, "Partner channel")

System_Boundary(s1, "Hotelio Platform") {
  Container(api, "API Gateway/BFF", "Kong", "Routing, auth, rate limit")
  Container(booking, "Booking Service", "Java/Spring", "Booking orchestration")
  Container(hotel, "Hotel Catalog Service", "Java/Spring", "Hotel data and availability")
  Container(userSvc, "User Service", "Java/Spring", "User status and profile")
  Container(promo, "Promo Service", "Java/Spring", "Promo validation")
  Container(review, "Review Service", "Java/Spring", "Reviews and trust score")
  Container(eventBus, "Event Bus", "Kafka/RabbitMQ", "Domain events")

  ContainerDb(bookingDb, "Booking DB", "PostgreSQL", "Bookings")
  ContainerDb(hotelDb, "Hotel DB", "PostgreSQL", "Hotels")
  ContainerDb(userDb, "User DB", "PostgreSQL", "Users")
  ContainerDb(promoDb, "Promo DB", "PostgreSQL", "Promos")
  ContainerDb(reviewDb, "Review DB", "PostgreSQL", "Reviews")
}

Rel(user, api, "Uses", "HTTPS")
Rel(partner, api, "Uses", "HTTPS")

Rel(api, booking, "Routes /api/bookings", "HTTPS")
Rel(api, hotel, "Routes /api/hotels", "HTTPS")
Rel(api, userSvc, "Routes /api/users", "HTTPS")
Rel(api, promo, "Routes /api/promos", "HTTPS")
Rel(api, review, "Routes /api/reviews", "HTTPS")

Rel(booking, userSvc, "Validate user", "HTTPS/gRPC")
Rel(booking, hotel, "Check availability", "HTTPS/gRPC")
Rel(booking, promo, "Validate promo", "HTTPS/gRPC")
Rel(booking, review, "Check trust", "HTTPS/gRPC")

Rel(booking, bookingDb, "Read/write", "JDBC")
Rel(hotel, hotelDb, "Read/write", "JDBC")
Rel(userSvc, userDb, "Read/write", "JDBC")
Rel(promo, promoDb, "Read/write", "JDBC")
Rel(review, reviewDb, "Read/write", "JDBC")

Rel(booking, eventBus, "Publish BookingCreated", "Async")
Rel(review, eventBus, "Publish ReviewAdded", "Async")
Rel(promo, eventBus, "Publish PromoUpdated", "Async")
Rel(hotel, eventBus, "Publish HotelAvailabilityChanged", "Async")

@enduml
```

#### Ключевые решения и дизайн
- Границы сервисов определены по доменным сущностям и текущим модулям монолита (Booking, Hotel, User, Promo, Review).
- API Gateway обеспечивает единый вход, обратную совместимость, аутентификацию и маршрутизацию к сервисам.
- Валидации в Booking выполняются синхронно; для согласованности используются таймауты, retries.
- Для обмена изменениями между сервисами применяются доменные события/сообщения (outbox/inbox) и eventual consistency.
- Запрещен прямой доступ к чужим таблицам; каждая команда владеет своей схемой данных для обеспечения независимости команд и упрощению отправки обновлений на прод.

#### План миграции на примере Hotel Service (Strangler Fig)
1. Ввести API Gateway и проксировать все текущие эндпоинты на монолит (без изменения контрактов).
2. Выделить отдельный сервис, создать его собственную БД и настроить синхронизацию данных из монолита.
3. Перенаправить через gateway все `/api/hotels/*` на новый сервис; настроить мониторинг и быстрый rollback.
4. Добавить в монолит адаптер, который для проверок отелей (operational/fully-booked) вызывает новый сервис; оставить fallback на монолитную БД на переходный период.
5. После стабилизации прекратить доступ монолита к таблице `hotel` и объявить hotel данные собственностью Hotel Catalog Service.
6. Далее последовательно выделять Review, Promo, User, и затем Booking, уменьшая зону ответственности монолита до полного вывода.

#### Первый модуль для миграции
Начать с **Hotel Catalog Service**:
- Высокая нагрузка чтения (поиск) и явный выигрыш от горизонтального масштабирования.
- Низкая связность: сервис почти не зависит от других модулей и имеет простые контракты.
- Минимальные риски для критического бизнес-потока: можно стартовать с read-only и постепенно включать использование в Booking.
- Позволяет быстро внедрить кэширование и оптимизацию поиска без влияния на остальные домены.

**Недостатки, ограничения, риски**
- Рост операционной сложности (оркестрация, мониторинг, CI/CD на сервис).
- Сетевая латентность и каскадные сбои при синхронных вызовах.
- Eventual consistency и необходимость обработки расхождений данных.
- Риски миграции данных и поддержки двух реализаций на этапе разделения.
- Требуются новые компетенции и инфраструктура (event bus, API gateway, tracing).