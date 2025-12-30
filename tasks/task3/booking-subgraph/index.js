import { ApolloServer } from '@apollo/server';
import { startStandaloneServer } from '@apollo/server/standalone';
import { buildSubgraphSchema } from '@apollo/subgraph';
import gql from 'graphql-tag';
import { GraphQLError } from 'graphql';
import * as grpc from '@grpc/grpc-js';
import * as protoLoader from '@grpc/proto-loader';
import { fileURLToPath } from 'node:url';

const PROTO_PATH = fileURLToPath(new URL('./booking.proto', import.meta.url));
const BOOKING_GRPC_HOST = process.env.BOOKING_GRPC_HOST || 'booking-service';
const BOOKING_GRPC_PORT = process.env.BOOKING_GRPC_PORT || '9090';

const typeDefs = gql`
  extend schema
    @link(
      url: "https://specs.apollo.dev/federation/v2.5"
      import: ["@key", "@external"]
    )

  type Booking @key(fields: "id") {
    id: ID!
    userId: ID!
    hotelId: ID!
    promoCode: String
    discountPercent: Float
    hotel: Hotel
  }

  extend type Hotel @key(fields: "id") {
    id: ID! @external
  }

  type Query {
    bookingsByUser(userId: ID!): [Booking!]!
  }
`;

const packageDefinition = protoLoader.loadSync(PROTO_PATH, {
  keepCase: false,
  longs: String,
  enums: String,
  defaults: true,
  oneofs: true,
});
const grpcDescriptor = grpc.loadPackageDefinition(packageDefinition);
const bookingGrpc = grpcDescriptor.booking?.BookingService;
const bookingClient = bookingGrpc
  ? new bookingGrpc(
      `${BOOKING_GRPC_HOST}:${BOOKING_GRPC_PORT}`,
      grpc.credentials.createInsecure()
    )
  : null;

console.log(
  `Booking gRPC target: ${BOOKING_GRPC_HOST}:${BOOKING_GRPC_PORT}`
);

const getHeader = (req, name) => {
  if (!req?.headers) {
    return null;
  }
  const key = name.toLowerCase();
  const value = req.headers[key] ?? req.headers[name];
  if (Array.isArray(value)) {
    return value[0];
  }
  return value ?? null;
};

const normalizeUserId = (value) =>
  typeof value === 'string' ? value.trim() : value;

const listBookings = (userId) =>
  new Promise((resolve, reject) => {
    if (!bookingClient) {
      reject(new Error('Booking gRPC client is not initialized'));
      return;
    }
    const handler = bookingClient.listBookings ?? bookingClient.ListBookings;
    if (!handler) {
      reject(new Error('Booking gRPC method ListBookings not found'));
      return;
    }
    handler.call(bookingClient, { userId }, (err, response) => {
      if (err) {
        reject(err);
        return;
      }
      resolve(response?.bookings ?? []);
    });
  });

const resolvers = {
  Query: {
    bookingsByUser: async (_, { userId }, { req }) => {
      const requestUserId = normalizeUserId(getHeader(req, 'userid'));
      const requestedUserId = normalizeUserId(userId);
      if (!requestUserId || requestUserId !== requestedUserId) {
        console.log(
          `ACL deny: header userid=${requestUserId ?? 'none'} request userId=${requestedUserId}`
        );
        throw new GraphQLError('Access denied', {
          extensions: { code: 'FORBIDDEN' },
        });
      }
      try {
        const bookings = await listBookings(requestedUserId);
        console.log(
          `Bookings fetched for userId=${requestedUserId}: ${bookings.length}`
        );
        return bookings.map((booking) => ({
          id: booking.id,
          userId: booking.userId,
          hotelId: booking.hotelId,
          promoCode: booking.promoCode || null,
          discountPercent: booking.discountPercent ?? null,
        }));
      } catch (error) {
        console.error('gRPC ListBookings failed:', error?.message ?? error);
        return [];
      }
    },
  },
  Booking: {
    hotel: (booking) => ({
      __typename: 'Hotel',
      id: booking.hotelId,
    }),
  },
};

const server = new ApolloServer({
  schema: buildSubgraphSchema([{ typeDefs, resolvers }]),
});

startStandaloneServer(server, {
  listen: { port: 4001 },
  context: async ({ req }) => ({ req }),
}).then(() => {
  console.log('Booking subgraph ready at http://localhost:4001/');
});
