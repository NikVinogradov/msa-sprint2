FROM debian:bullseye-slim

WORKDIR /app

# Install dependencies
RUN apt-get update && apt-get install -y \
    curl \
    postgresql-client \
    bash \
    && rm -rf /var/lib/apt/lists/*

COPY wait-for-it.sh /usr/local/bin/wait-for-it
COPY init-fixtures.sql regress.sh ./
COPY booking.sql regress.sh ./

RUN sed -i 's/\r$//' /app/regress.sh /usr/local/bin/wait-for-it \
 && sed -i '1s/^\xEF\xBB\xBF//' /app/regress.sh \
 && chmod +x /app/regress.sh /usr/local/bin/wait-for-it

RUN chmod +x /usr/local/bin/wait-for-it regress.sh

ENTRYPOINT ["./regress.sh"]
