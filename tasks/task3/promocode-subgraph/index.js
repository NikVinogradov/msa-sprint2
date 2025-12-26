import { ApolloServer } from '@apollo/server';
import { startStandaloneServer } from '@apollo/server/standalone';
import { buildSubgraphSchema } from '@apollo/subgraph';
import gql from 'graphql-tag';

const MONOLITH_URL = process.env.MONOLITH_URL || 'http://monolith:8080';

const typeDefs = gql`
  extend schema
    @link(
      url: "https://specs.apollo.dev/federation/v2.5"
      import: ["@key", "@external", "@override", "@requires"]
    )

  extend type Booking @key(fields: "id") {
    id: ID! @external
    promoCode: String @external
    userId: ID! @external
    discountPercent: Float!
      @override(from: "booking")
      @requires(fields: "promoCode userId")
    discountInfo: DiscountInfo @requires(fields: "promoCode userId")
  }

  type DiscountInfo {
    isValid: Boolean!
    originalDiscount: Float!
    finalDiscount: Float!
    description: String
    expiresAt: String
    applicableHotels: [ID!]!
  }

  type Query {
    validatePromoCode(code: String!, hotelId: ID): DiscountInfo!
    activePromoCodes: [DiscountInfo!]!
  }
`;

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

const parseBoolean = (value) => {
  if (typeof value === 'boolean') {
    return value;
  }
  if (typeof value === 'string') {
    const normalized = value.trim().toLowerCase();
    if (normalized === 'true') {
      return true;
    }
    if (normalized === 'false') {
      return false;
    }
  }
  return null;
};

const readJsonSafe = async (response) => {
  const text = await response.text();
  if (!text) {
    return null;
  }
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
};

const normalizePromo = (data, code) => {
  if (!data || typeof data !== 'object') {
    return null;
  }
  return {
    code: data.code ?? data.promoCode ?? code,
    discount:
      data.discount ??
      data.discountPercent ??
      data.discount_percent ??
      data.discount_value ??
      0,
    description: data.description ?? null,
    expired:
      data.expired ??
      data.isExpired ??
      data.expiredFlag ??
      data.is_expired ??
      null,
    validUntil:
      data.validUntil ??
      data.valid_until ??
      data.expiresAt ??
      data.expires_at ??
      null,
    applicableHotels:
      data.applicableHotels ?? data.applicable_hotels ?? data.hotels ?? [],
  };
};

const fetchPromo = async (code) => {
  const response = await fetch(
    `${MONOLITH_URL}/api/promos/${encodeURIComponent(code)}`
  );
  if (!response.ok) {
    return null;
  }
  const payload = await readJsonSafe(response);
  return normalizePromo(payload, code);
};

const fetchUserVip = async (userId) => {
  if (!userId) {
    return null;
  }
  const response = await fetch(
    `${MONOLITH_URL}/api/users/${encodeURIComponent(userId)}/vip`
  );
  if (!response.ok) {
    return null;
  }
  const payload = await readJsonSafe(response);
  if (typeof payload === 'object' && payload !== null) {
    return parseBoolean(payload.vip ?? payload.isVip ?? payload.value);
  }
  return parseBoolean(payload);
};

const fetchPromoValidity = async (code, isVipUser) => {
  const url = new URL(
    `${MONOLITH_URL}/api/promos/${encodeURIComponent(code)}/valid`
  );
  if (typeof isVipUser === 'boolean') {
    url.searchParams.set('isVipUser', String(isVipUser));
  }
  const response = await fetch(url);
  if (!response.ok) {
    return null;
  }
  const payload = await readJsonSafe(response);
  if (typeof payload === 'object' && payload !== null) {
    return parseBoolean(payload.valid ?? payload.isValid ?? payload.value);
  }
  return parseBoolean(payload);
};

const fetchValidatedPromo = async (code, userId) => {
  if (!userId) {
    return null;
  }
  const url = new URL(`${MONOLITH_URL}/api/promos/validate`);
  url.searchParams.set('code', code);
  url.searchParams.set('userId', userId);
  const response = await fetch(url, { method: 'POST' });
  if (!response.ok) {
    return null;
  }
  const payload = await readJsonSafe(response);
  return normalizePromo(payload, code);
};

const fetchPromoList = async () => {
  const response = await fetch(`${MONOLITH_URL}/api/promos`);
  if (!response.ok) {
    return [];
  }
  const payload = await readJsonSafe(response);
  if (Array.isArray(payload)) {
    return payload;
  }
  if (payload && Array.isArray(payload.items)) {
    return payload.items;
  }
  return [];
};

const buildDiscountInfo = ({
  promo,
  isValid,
  originalDiscount,
  finalDiscount,
}) => ({
  isValid: Boolean(isValid),
  originalDiscount: Number(originalDiscount) || 0,
  finalDiscount: Number(finalDiscount) || 0,
  description: promo?.description ?? null,
  expiresAt: promo?.validUntil ?? null,
  applicableHotels: Array.isArray(promo?.applicableHotels)
    ? promo.applicableHotels
    : [],
});

const resolvePromoInfo = async (code, userId, cache) => {
  if (!code) {
    return buildDiscountInfo({
      promo: null,
      isValid: false,
      originalDiscount: 0,
      finalDiscount: 0,
    });
  }

  const cacheKey = `${code}:${userId ?? ''}`;
  if (cache.has(cacheKey)) {
    return cache.get(cacheKey);
  }

  const [promoDetails, vipFlag, validatedPromo] = await Promise.all([
    fetchPromo(code),
    fetchUserVip(userId),
    fetchValidatedPromo(code, userId),
  ]);

  const promo = validatedPromo ?? promoDetails;
  let isValid = await fetchPromoValidity(code, vipFlag);
  if (isValid === null && promo?.expired !== null) {
    isValid = !promo.expired;
  }
  if (isValid === null) {
    isValid = Boolean(promo);
  }

  const originalDiscount = promo?.discount ?? 0;
  const finalDiscount = isValid ? originalDiscount : 0;

  const info = buildDiscountInfo({
    promo,
    isValid,
    originalDiscount,
    finalDiscount,
  });

  cache.set(cacheKey, info);
  return info;
};

const resolvers = {
  Booking: {
    discountPercent: async (booking, _, { promoCache }) => {
      const info = await resolvePromoInfo(
        booking.promoCode,
        booking.userId,
        promoCache
      );
      return info.finalDiscount;
    },
    discountInfo: async (booking, _, { promoCache }) =>
      resolvePromoInfo(booking.promoCode, booking.userId, promoCache),
  },
  Query: {
    validatePromoCode: async (_, { code }, { req, promoCache }) => {
      const userId = getHeader(req, 'userid');
      return resolvePromoInfo(code, userId, promoCache);
    },
    activePromoCodes: async () => {
      const promos = await fetchPromoList();
      return promos.map((item) => {
        const promo = normalizePromo(item, item?.code);
        const isValid =
          promo?.expired === null ? Boolean(promo) : !promo?.expired;
        const originalDiscount = promo?.discount ?? 0;
        const finalDiscount = isValid ? originalDiscount : 0;
        return buildDiscountInfo({
          promo,
          isValid,
          originalDiscount,
          finalDiscount,
        });
      });
    },
  },
};

const server = new ApolloServer({
  schema: buildSubgraphSchema([{ typeDefs, resolvers }]),
});

startStandaloneServer(server, {
  listen: { port: 4003 },
  context: async ({ req }) => ({
    req,
    promoCache: new Map(),
  }),
}).then(() => {
  console.log('Promocode subgraph ready at http://localhost:4003/');
});
