using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PRN222.RagAssistant.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHnswIndexOnDocumentChunksEmbedding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_DocumentChunks_Embedding_Hnsw""
                ON ""DocumentChunks"" USING hnsw (""Embedding"" vector_cosine_ops)
                WITH (m = 16, ef_construction = 64);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP INDEX IF EXISTS ""IX_DocumentChunks_Embedding_Hnsw"";
            ");
        }
    }
}