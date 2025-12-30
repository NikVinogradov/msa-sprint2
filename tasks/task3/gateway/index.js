import { ApolloServer } from '@apollo/server';
import { startStandaloneServer } from '@apollo/server/standalone';
import {
  ApolloGateway,
  IntrospectAndCompose,
  RemoteGraphQLDataSource,
} from '@apollo/gateway';

const gateway = new ApolloGateway({
  supergraphSdl: new IntrospectAndCompose({
    subgraphs: [
      { name: 'booking', url: 'http://booking-subgraph:4001' },
      { name: 'hotel', url: 'http://hotel-subgraph:4002' },
      { name: 'promocode', url: 'http://promocode-subgraph:4003' },
    ],
  }),
  buildService({ url }) {
    return new RemoteGraphQLDataSource({
      url,
      willSendRequest({ request, context }) {
        const headers = context?.req?.headers ?? {};
        Object.entries(headers).forEach(([key, value]) => {
          if (Array.isArray(value)) {
            value.forEach((entry) => request.http.headers.append(key, entry));
            return;
          }
          if (value !== undefined) {
            request.http.headers.set(key, value);
          }
        });
      },
    });
  },
});

const server = new ApolloServer({ gateway, subscriptions: false });

startStandaloneServer(server, {
  listen: { port: 4000 },
  context: async ({ req }) => ({ req }),
}).then(({ url }) => {
  console.log(`Gateway ready at ${url}`);
});
