FROM node:24-alpine AS build

WORKDIR /app

ARG VITE_API_URL=https://courseplatform-p8b0.onrender.com

COPY package*.json ./

RUN npm ci

COPY . .

ENV VITE_API_URL=${VITE_API_URL}

RUN npm run build


FROM nginx:alpine

COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]