import { ApolloServer } from '@apollo/server';
import { startStandaloneServer } from '@apollo/server/standalone';
import { buildSubgraphSchema } from '@apollo/subgraph';
import gql from 'graphql-tag';
import DataLoader from 'dataloader';

const MONOLITH_URL = process.env.MONOLITH_URL || 'http://monolith:8080';

const typeDefs = gql`
  extend schema
    @link(
      url: "https://specs.apollo.dev/federation/v2.5"
      import: ["@key"]
    )

  type Hotel @key(fields: "id") {
    id: ID!
    name: String
    city: String
    stars: Float
  }

  type Query {
    hotelsByIds(ids: [ID!]!): [Hotel]!
  }
`;

const toHotel = (data, id) => ({
  id: data?.id ?? id,
  name: data?.name ?? data?.title ?? data?.description ?? id,
  city: data?.city ?? null,
  stars: data?.stars ?? data?.rating ?? null,
});

const fetchHotel = async (id) => {
  if (!id) {
    return null;
  }
  const response = await fetch(
    `${MONOLITH_URL}/api/hotels/${encodeURIComponent(id)}`
  );
  if (!response.ok) {
    return null;
  }
  const data = await response.json();
  return toHotel(data, id);
};

const batchHotelsByIds = async (ids) => {
  const uniqueIds = [...new Set(ids.map((id) => String(id)))];
  const hotels = await Promise.all(uniqueIds.map((id) => fetchHotel(id)));
  const hotelById = new Map(
    hotels.filter(Boolean).map((hotel) => [String(hotel.id), hotel])
  );
  return ids.map((id) => hotelById.get(String(id)) ?? null);
};

const resolvers = {
  Hotel: {
    __resolveReference: async ({ id }, { loaders }) =>
      loaders.hotelById.load(id),
  },
  Query: {
    hotelsByIds: async (_, { ids }, { loaders }) => {
      const results = await loaders.hotelById.loadMany(ids);
      return results.map((item) => (item instanceof Error ? null : item));
    },
  },
};

const server = new ApolloServer({
  schema: buildSubgraphSchema([{ typeDefs, resolvers }]),
});

startStandaloneServer(server, {
  listen: { port: 4002 },
  context: async () => ({
    loaders: {
      hotelById: new DataLoader(batchHotelsByIds),
    },
  }),
}).then(() => {
  console.log('Hotel subgraph ready at http://localhost:4002/');
});
